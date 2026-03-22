using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

public partial class SftpSidebarViewModel : ObservableObject
{
    private SftpService? _sftpService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _hostLabel = "";

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
        try { await LoadRootAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP sidebar load error: {ex.Message}");
            IsConnected = false;
        }
    }

    private void Detach()
    {
        UnsubscribeNodes(RootNodes);
        RootNodes.Clear();
        _sftpService = null;
    }

    private async Task LoadRootAsync()
    {
        if (_sftpService == null || !_sftpService.IsConnected) return;

        IsLoading = true;
        RootNodes.Clear();

        try
        {
            var items = await _sftpService.ListDirectoryAsync("/");
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
        if (_sftpService == null || !node.IsDirectory || !node.HasDummyChild) return;

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
        await LoadRootAsync();
    }

    public async Task DownloadAndOpenAsync(SftpTreeNode node)
    {
        if (node.IsDirectory || _sftpService == null) return;

        var tempDir = Path.Combine(Path.GetTempPath(), "WinSTerm");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, node.Name);

        await _sftpService.DownloadFileAsync(
            node.FullPath, localPath, _ => { }, CancellationToken.None);
        Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
    }

    public async Task DeleteNodeAsync(SftpTreeNode node)
    {
        if (_sftpService == null) return;

        await _sftpService.DeleteAsync(node.FullPath);
        RemoveNodeFromTree(node);
    }

    public async Task RenameNodeAsync(SftpTreeNode node, string newName)
    {
        if (_sftpService == null) return;

        var parentPath = GetParentPath(node.FullPath);
        var newPath = parentPath.TrimEnd('/') + "/" + newName;

        await _sftpService.RenameAsync(node.FullPath, newPath);
        node.Name = newName;
        node.FullPath = newPath;
    }

    public async Task CreateFolderAsync(SftpTreeNode? parentNode, string folderName)
    {
        if (_sftpService == null) return;

        var parentPath = parentNode?.FullPath ?? "/";
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
            try { await LoadChildrenAsync(node); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SFTP expand error: {ex.Message}"); }
        }
    }

    private void RemoveNodeFromTree(SftpTreeNode target)
    {
        if (RootNodes.Remove(target)) return;

        foreach (var root in RootNodes)
        {
            if (RemoveFromChildren(root, target)) return;
        }
    }

    private static bool RemoveFromChildren(SftpTreeNode parent, SftpTreeNode target)
    {
        if (parent.Children.Remove(target)) return true;

        foreach (var child in parent.Children)
        {
            if (RemoveFromChildren(child, target)) return true;
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
