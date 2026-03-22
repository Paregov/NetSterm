using Renci.SshNet;
using Serilog;
using WinSTerm.Models;

namespace WinSTerm.Services;

public class SftpService : ISftpService
{
    private SftpClient? _sftpClient;

    public bool IsConnected => _sftpClient?.IsConnected == true;
    public string CurrentDirectory { get; set; } = "/";

    public Task ConnectAsync(Models.ConnectionInfo info, string? plainPassword = null)
    {
        return Task.Run(() =>
        {
            Log.Information("SFTP connecting to {Host}:{Port}", info.Host, info.Port);
            var connInfo = ConnectionFactory.Create(info, plainPassword);
            _sftpClient = new SftpClient(connInfo);
            _sftpClient.Connect();
            CurrentDirectory = _sftpClient.WorkingDirectory;
            Log.Information("SFTP connected successfully to {Host}", info.Host);
        });
    }

    public Task<List<SftpFileItem>> ListDirectoryAsync(string remotePath)
    {
        return Task.Run(() =>
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            var entries = _sftpClient.ListDirectory(remotePath);
            var items = new List<SftpFileItem>();

            foreach (var entry in entries)
            {
                if (entry.Name == "." || entry.Name == "..")
                    continue;

                items.Add(new SftpFileItem
                {
                    Name = entry.Name,
                    FullPath = entry.FullName,
                    IsDirectory = entry.IsDirectory,
                    Size = entry.Length,
                    LastModified = entry.LastWriteTimeUtc,
                    Permissions = entry.Attributes != null
                        ? FormatPermissions(entry.IsDirectory, entry.Attributes)
                        : "",
                    Owner = entry.UserId.ToString()
                });
            }

            return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name).ToList();
        });
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, Action<long> progressCallback, CancellationToken ct)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected.");

        await Task.Run(() =>
        {
            using var localStream = System.IO.File.Create(localPath);
            _sftpClient.DownloadFile(remotePath, localStream, downloaded =>
            {
                ct.ThrowIfCancellationRequested();
                progressCallback(unchecked((long)downloaded));
            });
        }, ct);
    }

    public async Task UploadFileAsync(string localPath, string remotePath, Action<long> progressCallback, CancellationToken ct)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected.");

        await Task.Run(() =>
        {
            using var localStream = System.IO.File.OpenRead(localPath);
            _sftpClient.UploadFile(localStream, remotePath, uploaded =>
            {
                ct.ThrowIfCancellationRequested();
                progressCallback(unchecked((long)uploaded));
            });
        }, ct);
    }

    public Task DeleteAsync(string remotePath)
    {
        return Task.Run(() =>
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            var attrs = _sftpClient.GetAttributes(remotePath);
            if (attrs.IsDirectory)
                _sftpClient.DeleteDirectory(remotePath);
            else
                _sftpClient.DeleteFile(remotePath);
        });
    }

    public Task CreateDirectoryAsync(string remotePath)
    {
        return Task.Run(() =>
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            _sftpClient.CreateDirectory(remotePath);
        });
    }

    public Task RenameAsync(string oldPath, string newPath)
    {
        return Task.Run(() =>
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            _sftpClient.RenameFile(oldPath, newPath);
        });
    }

    public void Disconnect()
    {
        Log.Information("SFTP disconnecting");
        if (_sftpClient != null)
        {
            if (_sftpClient.IsConnected)
                _sftpClient.Disconnect();
            _sftpClient.Dispose();
            _sftpClient = null;
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }

    private static string FormatPermissions(bool isDir, Renci.SshNet.Sftp.SftpFileAttributes a)
    {
        return string.Concat(
            isDir ? 'd' : '-',
            a.OwnerCanRead ? 'r' : '-', a.OwnerCanWrite ? 'w' : '-', a.OwnerCanExecute ? 'x' : '-',
            a.GroupCanRead ? 'r' : '-', a.GroupCanWrite ? 'w' : '-', a.GroupCanExecute ? 'x' : '-',
            a.OthersCanRead ? 'r' : '-', a.OthersCanWrite ? 'w' : '-', a.OthersCanExecute ? 'x' : '-');
    }
}
