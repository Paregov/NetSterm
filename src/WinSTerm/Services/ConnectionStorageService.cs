using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinSTerm.Models;

namespace WinSTerm.Services;

public class ConnectionStorageService : IConnectionStorageService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly object _lock = new();
    private ConnectionStore _store;

    public ConnectionStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "WinSTerm");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "connections.json");
        _store = Load();
    }

    public ConnectionStore Store => _store;

    private ConnectionStore Load()
    {
        if (!File.Exists(_filePath))
            return new ConnectionStore();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<ConnectionStore>(json, s_jsonOptions) ?? new ConnectionStore();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_store, s_jsonOptions);
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public void AddConnection(Models.ConnectionInfo connection)
    {
        lock (_lock)
        {
            _store.Connections.Add(connection);
            Save();
        }
    }

    public void UpdateConnection(Models.ConnectionInfo connection)
    {
        lock (_lock)
        {
            var index = _store.Connections.FindIndex(c => c.Id == connection.Id);
            if (index >= 0)
            {
                _store.Connections[index] = connection;
                Save();
            }
        }
    }

    public void DeleteConnection(string connectionId)
    {
        lock (_lock)
        {
            _store.Connections.RemoveAll(c => c.Id == connectionId);
            Save();
        }
    }

    public void AddFolder(ConnectionFolder folder)
    {
        lock (_lock)
        {
            _store.Folders.Add(folder);
            Save();
        }
    }

    public void DeleteFolder(string folderId)
    {
        lock (_lock)
        {
            DeleteFolderRecursive(folderId);
            Save();
        }
    }

    private void DeleteFolderRecursive(string folderId)
    {
        _store.Connections.RemoveAll(c => c.FolderId == folderId);

        var subFolders = _store.Folders.Where(f => f.ParentFolderId == folderId).ToList();
        foreach (var sub in subFolders)
            DeleteFolderRecursive(sub.Id);

        _store.Folders.RemoveAll(f => f.Id == folderId);
    }

    public List<Models.ConnectionInfo> GetConnectionsInFolder(string? folderId)
    {
        return _store.Connections.Where(c => c.FolderId == folderId).ToList();
    }

    public List<ConnectionFolder> GetSubFolders(string? parentFolderId)
    {
        return _store.Folders.Where(f => f.ParentFolderId == parentFolderId).ToList();
    }

    public static string EncryptPassword(string plaintext)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string DecryptPassword(string encrypted)
    {
        var bytes = Convert.FromBase64String(encrypted);
        var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }
}
