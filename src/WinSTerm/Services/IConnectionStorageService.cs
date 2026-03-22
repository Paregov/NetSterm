using WinSTerm.Models;

namespace WinSTerm.Services;

public interface IConnectionStorageService
{
    ConnectionStore Store { get; }
    void Save();
    void AddConnection(ConnectionInfo connection);
    void UpdateConnection(ConnectionInfo connection);
    void DeleteConnection(string connectionId);
    void AddFolder(ConnectionFolder folder);
    void DeleteFolder(string folderId);
    List<ConnectionInfo> GetConnectionsInFolder(string? folderId);
    List<ConnectionFolder> GetSubFolders(string? parentFolderId);
}
