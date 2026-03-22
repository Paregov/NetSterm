using WinSTerm.Models;

namespace WinSTerm.Services;

public interface ISftpService : IDisposable
{
    bool IsConnected { get; }
    string CurrentDirectory { get; set; }

    Task ConnectAsync(ConnectionInfo info, string? plainPassword = null);
    Task<List<SftpFileItem>> ListDirectoryAsync(string remotePath);
    Task DownloadFileAsync(string remotePath, string localPath, Action<long> progressCallback, CancellationToken ct);
    Task UploadFileAsync(string localPath, string remotePath, Action<long> progressCallback, CancellationToken ct);
    Task DeleteAsync(string remotePath);
    Task CreateDirectoryAsync(string remotePath);
    Task RenameAsync(string oldPath, string newPath);
    void Disconnect();
}
