using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinSTerm.Models;

namespace WinSTerm.Services;

/// <summary>
/// Handles export/import of WinSTerm configuration as ZIP packages.
/// Designed for reuse with future central server sync.
/// </summary>
public class ConfigurationExportService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConnectionStorageService _connectionStorage;
    private readonly SnippetStorageService _snippetStorage;

    public ConfigurationExportService(
        ConnectionStorageService connectionStorage,
        SnippetStorageService snippetStorage)
    {
        _connectionStorage = connectionStorage;
        _snippetStorage = snippetStorage;
    }

    /// <summary>Export configuration to a ZIP file.</summary>
    public void Export(string zipFilePath, ExportOptions options)
    {
        if (File.Exists(zipFilePath))
            File.Delete(zipFilePath);

        using var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);

        var manifest = new ExportManifest { ExportedAt = DateTime.UtcNow };

        if (options.IncludeConnections)
            ExportConnections(zip, options, manifest);

        if (options.IncludeSnippets)
            ExportSnippets(zip, options, manifest);

        WriteJsonEntry(zip, "manifest.json", manifest);
    }

    /// <summary>Import configuration from a ZIP file.</summary>
    public ImportResult Import(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException("Import file not found.", zipFilePath);

        var result = new ImportResult();

        using var zip = ZipFile.OpenRead(zipFilePath);

        var manifest = ReadJsonEntry<ExportManifest>(zip, "manifest.json");
        if (manifest != null)
        {
            result.ContainedPasswords = manifest.ContainsPasswords;
            result.ContainedPrivateKeys = manifest.ContainsPrivateKeys;
        }

        var connData = ReadJsonEntry<ConnectionExportData>(zip, "connections.json");
        if (connData != null)
        {
            var (conns, folders) = ImportConnections(connData);
            result.ConnectionsAdded = conns;
            result.ConnectionFoldersAdded = folders;
        }

        var snippetData = ReadJsonEntry<SnippetExportData>(zip, "snippets.json");
        if (snippetData != null)
        {
            var (snippets, folders) = ImportSnippets(snippetData);
            result.SnippetsAdded = snippets;
            result.SnippetFoldersAdded = folders;
        }

        result.PrivateKeysImported = ImportPrivateKeys(zip);

        if (result.PrivateKeysImported > 0)
        {
            var keysDir = GetKeysDirectory();
            UpdatePrivateKeyPaths(keysDir);
        }

        return result;
    }

    // --- Export helpers ---

    private void ExportConnections(ZipArchive zip, ExportOptions options, ExportManifest manifest)
    {
        var connStore = _connectionStorage.Store;
        var folders = FilterItems(connStore.Folders, options.SelectedConnectionFolderIds, f => f.Id);
        var connections = FilterItems(connStore.Connections, options.SelectedConnectionIds, c => c.Id);

        if (options.IncludePasswords)
        {
            connections = connections.Select(DecryptPasswordForExport).ToList();
            manifest.ContainsPasswords = connections.Any(c => !string.IsNullOrEmpty(c.EncryptedPassword));
        }
        else
        {
            connections = connections.Select(StripPassword).ToList();
        }

        var data = new ConnectionExportData { Folders = folders, Connections = connections };
        WriteJsonEntry(zip, "connections.json", data);
        manifest.ConnectionCount = connections.Count;
        manifest.ConnectionFolderCount = folders.Count;

        if (options.IncludePrivateKeys)
            ExportPrivateKeys(zip, connections, manifest);
    }

    private static void ExportPrivateKeys(
        ZipArchive zip,
        List<Models.ConnectionInfo> connections,
        ExportManifest manifest)
    {
        var keyPaths = connections
            .Where(c => !string.IsNullOrEmpty(c.PrivateKeyPath) && File.Exists(c.PrivateKeyPath))
            .Select(c => c.PrivateKeyPath!)
            .Distinct()
            .ToList();

        foreach (var keyPath in keyPaths)
        {
            var keyFileName = Path.GetFileName(keyPath);
            zip.CreateEntryFromFile(keyPath, $"keys/{keyFileName}");
        }

        if (keyPaths.Count > 0)
            manifest.ContainsPrivateKeys = true;
    }

    private void ExportSnippets(ZipArchive zip, ExportOptions options, ExportManifest manifest)
    {
        var snippetStore = _snippetStorage.Store;
        var folders = FilterItems(snippetStore.Folders, options.SelectedSnippetFolderIds, f => f.Id);
        var snippets = FilterItems(snippetStore.Snippets, options.SelectedSnippetIds, s => s.Id);

        var data = new SnippetExportData { Folders = folders, Snippets = snippets };
        WriteJsonEntry(zip, "snippets.json", data);
        manifest.SnippetCount = snippets.Count;
        manifest.SnippetFolderCount = folders.Count;
    }

    // --- Import helpers ---

    private (int connections, int folders) ImportConnections(ConnectionExportData data)
    {
        var store = _connectionStorage.Store;
        var folderIdMap = new Dictionary<string, string>();
        int foldersAdded = 0;

        foreach (var importedFolder in data.Folders)
        {
            var parentId = RemapId(importedFolder.ParentFolderId, folderIdMap);
            var existing = store.Folders.FirstOrDefault(f =>
                f.Name == importedFolder.Name && f.ParentFolderId == parentId);

            if (existing != null)
            {
                folderIdMap[importedFolder.Id] = existing.Id;
                continue;
            }

            var newId = Guid.NewGuid().ToString();
            folderIdMap[importedFolder.Id] = newId;
            store.Folders.Add(new ConnectionFolder
            {
                Id = newId,
                Name = importedFolder.Name,
                ParentFolderId = parentId,
                IsExpanded = importedFolder.IsExpanded
            });
            foldersAdded++;
        }

        int connectionsAdded = 0;
        foreach (var imported in data.Connections)
        {
            var remappedFolderId = RemapId(imported.FolderId, folderIdMap);
            var isDuplicate = store.Connections.Any(c =>
                c.Host == imported.Host
                && c.Port == imported.Port
                && c.Username == imported.Username
                && c.FolderId == remappedFolderId);

            if (isDuplicate)
                continue;

            var newConn = CloneConnection(imported);
            newConn.Id = Guid.NewGuid().ToString();
            newConn.FolderId = remappedFolderId;
            newConn.CreatedAt = DateTime.UtcNow;
            newConn.LastConnectedAt = default;

            if (!string.IsNullOrEmpty(imported.EncryptedPassword))
            {
                try
                {
                    newConn.EncryptedPassword = ConnectionStorageService.EncryptPassword(
                        imported.EncryptedPassword);
                }
                catch
                {
                    newConn.EncryptedPassword = null;
                }
            }

            store.Connections.Add(newConn);
            connectionsAdded++;
        }

        if (foldersAdded > 0 || connectionsAdded > 0)
            _connectionStorage.Save();

        return (connectionsAdded, foldersAdded);
    }

    private (int snippets, int folders) ImportSnippets(SnippetExportData data)
    {
        var store = _snippetStorage.Store;
        var folderIdMap = new Dictionary<string, string>();
        int foldersAdded = 0;

        foreach (var importedFolder in data.Folders)
        {
            var parentId = RemapId(importedFolder.ParentFolderId, folderIdMap);
            var existing = store.Folders.FirstOrDefault(f =>
                f.Name == importedFolder.Name && f.ParentFolderId == parentId);

            if (existing != null)
            {
                folderIdMap[importedFolder.Id] = existing.Id;
                continue;
            }

            var newId = Guid.NewGuid().ToString();
            folderIdMap[importedFolder.Id] = newId;
            store.Folders.Add(new SnippetFolder
            {
                Id = newId,
                Name = importedFolder.Name,
                ParentFolderId = parentId,
                IsExpanded = importedFolder.IsExpanded
            });
            foldersAdded++;
        }

        int snippetsAdded = 0;
        foreach (var imported in data.Snippets)
        {
            var remappedFolderId = RemapId(imported.FolderId, folderIdMap);
            var isDuplicate = store.Snippets.Any(s =>
                s.Name == imported.Name
                && s.Command == imported.Command
                && s.FolderId == remappedFolderId);

            if (isDuplicate)
                continue;

            var newSnippet = new CommandSnippet
            {
                Id = Guid.NewGuid().ToString(),
                Name = imported.Name,
                Command = imported.Command,
                Description = imported.Description,
                FolderId = remappedFolderId,
                CreatedAt = DateTime.UtcNow
            };

            store.Snippets.Add(newSnippet);
            snippetsAdded++;
        }

        if (foldersAdded > 0 || snippetsAdded > 0)
            _snippetStorage.Save();

        return (snippetsAdded, foldersAdded);
    }

    private static int ImportPrivateKeys(ZipArchive zip)
    {
        var keysDir = GetKeysDirectory();
        Directory.CreateDirectory(keysDir);

        int imported = 0;
        foreach (var entry in zip.Entries.Where(e =>
            e.FullName.StartsWith("keys/", StringComparison.Ordinal) && e.Name.Length > 0))
        {
            var destPath = Path.Combine(keysDir, entry.Name);
            if (!File.Exists(destPath))
            {
                entry.ExtractToFile(destPath);
                imported++;
            }
        }

        return imported;
    }

    private void UpdatePrivateKeyPaths(string keysDir)
    {
        var store = _connectionStorage.Store;
        bool changed = false;

        foreach (var conn in store.Connections)
        {
            if (string.IsNullOrEmpty(conn.PrivateKeyPath))
                continue;
            if (File.Exists(conn.PrivateKeyPath))
                continue;

            var keyName = Path.GetFileName(conn.PrivateKeyPath);
            var localPath = Path.Combine(keysDir, keyName);
            if (File.Exists(localPath))
            {
                conn.PrivateKeyPath = localPath;
                changed = true;
            }
        }

        if (changed)
            _connectionStorage.Save();
    }

    // --- Filtering ---

    private static List<T> FilterItems<T>(List<T> all, HashSet<string> selected, Func<T, string> idSelector)
    {
        if (selected.Count == 0)
            return all.ToList();
        return all.Where(item => selected.Contains(idSelector(item))).ToList();
    }

    // --- Password handling ---

    private static Models.ConnectionInfo StripPassword(Models.ConnectionInfo source)
    {
        var clone = CloneConnection(source);
        clone.EncryptedPassword = null;
        return clone;
    }

    private static Models.ConnectionInfo DecryptPasswordForExport(Models.ConnectionInfo source)
    {
        var clone = CloneConnection(source);
        if (!string.IsNullOrEmpty(source.EncryptedPassword))
        {
            try
            {
                clone.EncryptedPassword = ConnectionStorageService.DecryptPassword(
                    source.EncryptedPassword);
            }
            catch
            {
                clone.EncryptedPassword = null;
            }
        }
        return clone;
    }

    private static Models.ConnectionInfo CloneConnection(Models.ConnectionInfo source)
    {
        var json = JsonSerializer.Serialize(source, s_jsonOptions);
        return JsonSerializer.Deserialize<Models.ConnectionInfo>(json, s_jsonOptions)!;
    }

    // --- Shared helpers ---

    private static string? RemapId(string? oldId, Dictionary<string, string> map)
    {
        if (oldId == null)
            return null;
        return map.TryGetValue(oldId, out var newId) ? newId : null;
    }

    private static string GetKeysDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinSTerm", "keys");
    }

    private static void WriteJsonEntry<T>(ZipArchive zip, string entryName, T data)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, data, s_jsonOptions);
    }

    private static T? ReadJsonEntry<T>(ZipArchive zip, string entryName) where T : class
    {
        var entry = zip.GetEntry(entryName);
        if (entry == null)
            return null;
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, s_jsonOptions);
    }
}

/// <summary>DTO for deserializing connection data from ZIP entries.</summary>
public class ConnectionExportData
{
    public List<ConnectionFolder> Folders { get; set; } = [];
    public List<Models.ConnectionInfo> Connections { get; set; } = [];
}

/// <summary>DTO for deserializing snippet data from ZIP entries.</summary>
public class SnippetExportData
{
    public List<SnippetFolder> Folders { get; set; } = [];
    public List<CommandSnippet> Snippets { get; set; } = [];
}
