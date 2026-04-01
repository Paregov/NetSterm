using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSterm.Services;

namespace NetSterm.ViewModels;

public partial class SftpSidebarViewModel : ObservableObject
{
    private SftpService? _sftpService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _hostLabel = "";
    [ObservableProperty] private string _currentPath = "/";

    public ObservableCollection<SftpTreeNode> RootNodes { get; } = [];

    public async void AttachToTab(SessionTabViewModel? tab)
    {
        Detach();

        if (tab == null || !tab.IsConnected || !tab.SftpService.IsConnected)
        {
            IsConnected = false;
            HostLabel = "";
            return;
        }

        _sftpService = tab.SftpService;
        IsConnected = true;
        HostLabel = tab.ConnectionInfo.Host;
        try
        { await LoadDirectoryAsync(_sftpService.CurrentDirectory); }
        catch (Exception ex)
        {
            Debug.WriteLine($"SFTP sidebar load error: {ex.Message}");
            IsConnected = false;
        }
    }

    private void Detach()
    {
        UnsubscribeNodes(RootNodes);
        RootNodes.Clear();
        _sftpService = null;
    }

    public async Task LoadDirectoryAsync(string? path = null)
    {
        if (_sftpService == null || !_sftpService.IsConnected)
            return;

        var targetPath = path ?? _sftpService.CurrentDirectory ?? "/";
        CurrentPath = targetPath;
        IsLoading = true;
        UnsubscribeNodes(RootNodes);
        RootNodes.Clear();

        try
        {
            var items = await _sftpService.ListDirectoryAsync(targetPath);
            foreach (var item in items)
            {
                var node = item.IsDirectory
                    ? SftpTreeNode.CreateDirectory(item.Name, item.FullPath)
                    : SftpTreeNode.CreateFile(item.Name, item.FullPath, item.Size);
                SubscribeNode(node);
                RootNodes.Add(node);
            }
        }
        catch
        {
            RootNodes.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadChildrenAsync(SftpTreeNode node)
    {
        if (_sftpService == null || !node.IsDirectory || !node.HasDummyChild)
            return;

        node.IsLoading = true;
        try
        {
            var items = await _sftpService.ListDirectoryAsync(node.FullPath);
            node.Children.Clear();
            foreach (var item in items)
            {
                var child = item.IsDirectory
                    ? SftpTreeNode.CreateDirectory(item.Name, item.FullPath)
                    : SftpTreeNode.CreateFile(item.Name, item.FullPath, item.Size);
                SubscribeNode(child);
                node.Children.Add(child);
            }
        }
        catch
        {
            node.Children.Clear();
            node.Children.Add(new SftpTreeNode
            {
                Name = "Error loading contents",
                FullPath = SftpTreeNode.DummySentinel
            });
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadDirectoryAsync(CurrentPath);
    }

    [RelayCommand]
    private async Task NavigateUp()
    {
        if (CurrentPath == "/")
            return;
        var parent = GetParentPath(CurrentPath);
        await LoadDirectoryAsync(parent);
    }

    [RelayCommand]
    private async Task NavigateTo(string path)
    {
        await LoadDirectoryAsync(path);
    }

    public async Task UploadFilesAsync(string[] localPaths, string remoteDirectory)
    {
        if (_sftpService == null || !_sftpService.IsConnected)
            return;

        IsLoading = true;
        try
        {
            foreach (var localPath in localPaths)
            {
                if (Directory.Exists(localPath))
                {
                    await UploadDirectoryAsync(localPath, remoteDirectory);
                }
                else if (File.Exists(localPath))
                {
                    var fileName = Path.GetFileName(localPath);
                    var remotePath = remoteDirectory.TrimEnd('/') + "/" + fileName;
                    await _sftpService.UploadFileAsync(localPath, remotePath, _ => { }, CancellationToken.None);
                }
            }

            await LoadDirectoryAsync(CurrentPath);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UploadDirectoryAsync(string localDirPath, string remoteParentDir)
    {
        var dirName = Path.GetFileName(localDirPath);
        var remoteDirPath = remoteParentDir.TrimEnd('/') + "/" + dirName;

        try
        { await _sftpService!.CreateDirectoryAsync(remoteDirPath); }
        catch { /* directory may already exist */ }

        foreach (var file in Directory.GetFiles(localDirPath))
        {
            var fileName = Path.GetFileName(file);
            var remotePath = remoteDirPath + "/" + fileName;
            await _sftpService!.UploadFileAsync(file, remotePath, _ => { }, CancellationToken.None);
        }

        foreach (var subDir in Directory.GetDirectories(localDirPath))
        {
            await UploadDirectoryAsync(subDir, remoteDirPath);
        }
    }

    public async Task DownloadAndOpenAsync(SftpTreeNode node)
    {
        if (node.IsDirectory || _sftpService == null)
            return;

        var tempDir = Path.Combine(Path.GetTempPath(), "NetSterm");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, node.Name);

        await _sftpService.DownloadFileAsync(
            node.FullPath, localPath, _ => { }, CancellationToken.None);
        Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
    }

    public async Task DeleteNodeAsync(SftpTreeNode node)
    {
        if (_sftpService == null)
            return;

        await _sftpService.DeleteAsync(node.FullPath);
        RemoveNodeFromTree(node);
    }

    public async Task RenameNodeAsync(SftpTreeNode node, string newName)
    {
        if (_sftpService == null)
            return;

        var parentPath = GetParentPath(node.FullPath);
        var newPath = parentPath.TrimEnd('/') + "/" + newName;

        await _sftpService.RenameAsync(node.FullPath, newPath);
        node.Name = newName;
        node.FullPath = newPath;
    }

    public async Task CreateFolderAsync(SftpTreeNode? parentNode, string folderName)
    {
        if (_sftpService == null)
            return;

        var parentPath = parentNode?.FullPath ?? CurrentPath;
        var fullPath = parentPath.TrimEnd('/') + "/" + folderName;

        await _sftpService.CreateDirectoryAsync(fullPath);

        var newNode = SftpTreeNode.CreateDirectory(folderName, fullPath);
        SubscribeNode(newNode);

        if (parentNode != null)
        {
            if (parentNode.HasDummyChild)
                parentNode.Children.Clear();
            parentNode.Children.Insert(0, newNode);
            parentNode.IsExpanded = true;
        }
        else
        {
            RootNodes.Insert(0, newNode);
        }
    }

    private void SubscribeNode(SftpTreeNode node)
    {
        node.ExpandRequested += OnNodeExpandRequested;
    }

    private void UnsubscribeNodes(IEnumerable<SftpTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.ExpandRequested -= OnNodeExpandRequested;
            UnsubscribeNodes(node.Children);
        }
    }

    private async void OnNodeExpandRequested(object? sender, EventArgs e)
    {
        if (sender is SftpTreeNode node)
        {
            try
            { await LoadChildrenAsync(node); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SFTP expand error: {ex.Message}"); }
        }
    }

    private void RemoveNodeFromTree(SftpTreeNode target)
    {
        if (RootNodes.Remove(target))
            return;

        foreach (var root in RootNodes)
        {
            if (RemoveFromChildren(root, target))
                return;
        }
    }

    private static bool RemoveFromChildren(SftpTreeNode parent, SftpTreeNode target)
    {
        if (parent.Children.Remove(target))
            return true;

        foreach (var child in parent.Children)
        {
            if (RemoveFromChildren(child, target))
                return true;
        }

        return false;
    }

    private static string GetParentPath(string fullPath)
    {
        var trimmed = fullPath.TrimEnd('/');
        var idx = trimmed.LastIndexOf('/');
        return idx <= 0 ? "/" : trimmed[..idx];
    }
}
