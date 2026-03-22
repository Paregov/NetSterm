using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using WinSTerm.Models;

namespace WinSTerm.Services;

public class ConnectionStorageService
{
    private readonly string _filePath;
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
        return JsonConvert.DeserializeObject<ConnectionStore>(json) ?? new ConnectionStore();
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(_store, Formatting.Indented);
        File.WriteAllText(_filePath, json);
    }

    public void AddConnection(Models.ConnectionInfo connection)
    {
        _store.Connections.Add(connection);
        Save();
    }

    public void UpdateConnection(Models.ConnectionInfo connection)
    {
        var index = _store.Connections.FindIndex(c => c.Id == connection.Id);
        if (index >= 0)
        {
            _store.Connections[index] = connection;
            Save();
        }
    }

    public void DeleteConnection(string connectionId)
    {
        _store.Connections.RemoveAll(c => c.Id == connectionId);
        Save();
    }

    public void AddFolder(ConnectionFolder folder)
    {
        _store.Folders.Add(folder);
        Save();
    }

    public void DeleteFolder(string folderId)
    {
        // Delete all connections in this folder
        _store.Connections.RemoveAll(c => c.FolderId == folderId);

        // Recursively delete subfolders
        var subFolders = _store.Folders.Where(f => f.ParentFolderId == folderId).ToList();
        foreach (var sub in subFolders)
            DeleteFolder(sub.Id);

        _store.Folders.RemoveAll(f => f.Id == folderId);
        Save();
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
