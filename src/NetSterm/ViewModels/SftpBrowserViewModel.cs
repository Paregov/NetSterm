using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSterm.Models;
using NetSterm.Services;

namespace NetSterm.ViewModels;

public partial class SftpBrowserViewModel : ObservableObject
{
    private SftpService? _sftpService;

    [ObservableProperty]
    private ObservableCollection<SftpFileItem> _remoteFiles = [];

    [ObservableProperty]
    private ObservableCollection<SftpFileItem> _localFiles = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateRemoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateRemoteUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshRemoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRemoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateRemoteFolderCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _remotePath = "/";

    [ObservableProperty]
    private string _localPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [ObservableProperty]
    private ObservableCollection<TransferItem> _transfers = [];

    // Selected items set from code-behind
    public IList<SftpFileItem> SelectedLocalFiles { get; set; } = new List<SftpFileItem>();
    public IList<SftpFileItem> SelectedRemoteFiles { get; set; } = new List<SftpFileItem>();

    public void AttachService(SftpService service)
    {
        _sftpService = service;
        IsConnected = service.IsConnected;
        RemotePath = service.CurrentDirectory;
        _ = LoadRemoteFilesAsync();
        _ = LoadLocalFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task NavigateRemote(string path)
    {
        RemotePath = path;
        await LoadRemoteFilesAsync();
    }

    [RelayCommand]
    private async Task NavigateLocal(string path)
    {
        LocalPath = path;
        await LoadLocalFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task Upload()
    {
        if (_sftpService is null) return;

        var files = SelectedLocalFiles.Where(f => !f.IsDirectory).ToList();
        if (files.Count == 0) return;

        foreach (var file in files)
        {
            var remoteDest = RemotePath.TrimEnd('/') + "/" + file.Name;
            var transfer = new TransferItem
            {
                FileName = file.Name,
                LocalPath = file.FullPath,
                RemotePath = remoteDest,
                Direction = TransferDirection.Upload,
                TotalBytes = file.Size,
                Status = TransferStatus.InProgress
            };
            Transfers.Add(transfer);

            try
            {
                await _sftpService.UploadFileAsync(
                    file.FullPath, remoteDest,
                    bytes => transfer.TransferredBytes = bytes,
                    CancellationToken.None);
                transfer.Status = TransferStatus.Completed;
            }
            catch
            {
                transfer.Status = TransferStatus.Failed;
            }
        }

        await LoadRemoteFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task Download()
    {
        if (_sftpService is null) return;

        var files = SelectedRemoteFiles.Where(f => !f.IsDirectory).ToList();
        if (files.Count == 0) return;

        foreach (var file in files)
        {
            var localDest = Path.Combine(LocalPath, file.Name);
            var transfer = new TransferItem
            {
                FileName = file.Name,
                LocalPath = localDest,
                RemotePath = file.FullPath,
                Direction = TransferDirection.Download,
                TotalBytes = file.Size,
                Status = TransferStatus.InProgress
            };
            Transfers.Add(transfer);

            try
            {
                await _sftpService.DownloadFileAsync(
                    file.FullPath, localDest,
                    bytes => transfer.TransferredBytes = bytes,
                    CancellationToken.None);
                transfer.Status = TransferStatus.Completed;
            }
            catch
            {
                transfer.Status = TransferStatus.Failed;
            }
        }

        await LoadLocalFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task DeleteRemote(SftpFileItem? item)
    {
        if (_sftpService is null || item is null) return;

        await _sftpService.DeleteAsync(item.FullPath);
        await LoadRemoteFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task CreateRemoteFolder(string? folderName)
    {
        if (_sftpService is null || string.IsNullOrWhiteSpace(folderName)) return;

        var fullPath = RemotePath.TrimEnd('/') + "/" + folderName;
        await _sftpService.CreateDirectoryAsync(fullPath);
        await LoadRemoteFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task RefreshRemote()
    {
        await LoadRemoteFilesAsync();
    }

    [RelayCommand]
    private async Task RefreshLocal()
    {
        await LoadLocalFilesAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task NavigateRemoteUp()
    {
        if (RemotePath is "/" or "") return;

        var parent = RemotePath.TrimEnd('/');
        var idx = parent.LastIndexOf('/');
        RemotePath = idx <= 0 ? "/" : parent[..idx];
        await LoadRemoteFilesAsync();
    }

    [RelayCommand]
    private async Task NavigateLocalUp()
    {
        var parent = Directory.GetParent(LocalPath);
        if (parent is null) return;

        LocalPath = parent.FullName;
        await LoadLocalFilesAsync();
    }

    private async Task LoadRemoteFilesAsync()
    {
        if (_sftpService is null || !IsConnected) return;

        IsBusy = true;
        try
        {
            var items = await _sftpService.ListDirectoryAsync(RemotePath);
            RemoteFiles = new ObservableCollection<SftpFileItem>(items);
        }
        catch
        {
            RemoteFiles.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task LoadLocalFilesAsync()
    {
        IsBusy = true;
        try
        {
            var items = new List<SftpFileItem>();

            if (!Directory.Exists(LocalPath)) return Task.CompletedTask;

            var dirInfo = new DirectoryInfo(LocalPath);

            foreach (var dir in dirInfo.GetDirectories())
            {
                try
                {
                    items.Add(new SftpFileItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        LastModified = dir.LastWriteTimeUtc
                    });
                }
                catch { /* skip inaccessible */ }
            }

            foreach (var file in dirInfo.GetFiles())
            {
                try
                {
                    items.Add(new SftpFileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTimeUtc
                    });
                }
                catch { /* skip inaccessible */ }
            }

            LocalFiles = new ObservableCollection<SftpFileItem>(
                items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name));
        }
        catch
        {
            LocalFiles.Clear();
        }
        finally
        {
            IsBusy = false;
        }

        return Task.CompletedTask;
    }
}
