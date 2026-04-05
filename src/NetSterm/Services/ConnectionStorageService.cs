using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetSterm.Models;
using Serilog;

namespace NetSterm.Services;

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
        var dir = Path.Combine(appData, "NetSterm");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "connections.json");
        _store = Load();
    }

    public ConnectionStore Store => _store;

    private ConnectionStore Load()
    {
        if (!File.Exists(_filePath))
        {
            Log.Debug("No connections file found, creating new store");
            return new ConnectionStore();
        }

        try
        {
            Log.Debug("Loading connections from {FilePath}", _filePath);
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ConnectionStore>(json, s_jsonOptions) ?? new ConnectionStore();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load connections from {FilePath}", _filePath);
            return new ConnectionStore();
        }
    }

    public void Save()
    {
        Log.Debug("Saving connections to {FilePath}", _filePath);
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

    public void AddFolders(IEnumerable<ConnectionFolder> folders)
    {
        lock (_lock)
        {
            _store.Folders.AddRange(folders);
            Save();
        }
    }

    public void AddConnections(IEnumerable<ConnectionInfo> connections)
    {
        lock (_lock)
        {
            _store.Connections.AddRange(connections);
            Save();
        }
    }

    public static string EncryptPassword(string plaintext)
    {
        return EncryptionService.Encrypt(plaintext);
    }

    public static string DecryptPassword(string encrypted)
    {
        return EncryptionService.Decrypt(encrypted);
    }
}
