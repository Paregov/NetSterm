using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConnectionStorageService _storage = new();

    // Session tree (left sidebar)
    public ObservableCollection<SessionTreeItem> SessionTree { get; } = new();

    // Open tabs
    public ObservableCollection<SessionTabViewModel> Tabs { get; } = new();

    [ObservableProperty] private SessionTabViewModel? _selectedTab;

    // Quick connect fields
    [ObservableProperty] private string _quickHost = "";
    [ObservableProperty] private string _quickUsername = "";
    [ObservableProperty] private int _quickPort = 22;

    // Status bar
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private int _connectionCount;

    public MainViewModel()
    {
        LoadSessionTree();
    }

    public void LoadSessionTree()
    {
        SessionTree.Clear();
        var store = _storage.Store;

        var rootItems = new List<SessionTreeItem>();

        // Add folders
        var folderMap = new Dictionary<string, SessionTreeItem>();
        foreach (var folder in store.Folders)
        {
            var item = new SessionTreeItem
            {
                Id = folder.Id,
                Name = folder.Name,
                IsFolder = true,
                IsExpanded = folder.IsExpanded
            };
            folderMap[folder.Id] = item;
        }

        // Nest folders
        foreach (var folder in store.Folders)
        {
            if (folder.ParentFolderId != null && folderMap.ContainsKey(folder.ParentFolderId))
                folderMap[folder.ParentFolderId].Children.Add(folderMap[folder.Id]);
            else
                rootItems.Add(folderMap[folder.Id]);
        }

        // Add connections
        foreach (var conn in store.Connections)
        {
            var item = new SessionTreeItem
            {
                Id = conn.Id,
                Name = conn.Name,
                IsFolder = false,
                ConnectionInfo = conn
            };

            if (conn.FolderId != null && folderMap.ContainsKey(conn.FolderId))
                folderMap[conn.FolderId].Children.Add(item);
            else
                rootItems.Add(item);
        }

        foreach (var item in rootItems)
            SessionTree.Add(item);
    }

    [RelayCommand]
    private async Task QuickConnect()
    {
        if (string.IsNullOrWhiteSpace(QuickHost)) return;

        var info = new ConnectionInfo
        {
            Name = $"{QuickUsername}@{QuickHost}",
            Host = QuickHost,
            Port = QuickPort,
            Username = string.IsNullOrWhiteSpace(QuickUsername) ? "root" : QuickUsername,
            AuthMethod = AuthMethod.Password
        };

        await OpenSession(info);
    }

    [RelayCommand]
    private async Task ConnectSession(SessionTreeItem? item)
    {
        if (item?.ConnectionInfo == null) return;
        await OpenSession(item.ConnectionInfo);
    }

    public async Task OpenSession(ConnectionInfo info)
    {
        // Check if already open
        var existingTab = Tabs.FirstOrDefault(t => t.ConnectionInfo.Id == info.Id);
        if (existingTab != null)
        {
            SelectedTab = existingTab;
            return;
        }

        var tab = new SessionTabViewModel(info);
        Tabs.Add(tab);
        SelectedTab = tab;
        ConnectionCount = Tabs.Count;
        StatusMessage = $"Connecting to {info.Host}...";

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void CloseTab(SessionTabViewModel? tab)
    {
        if (tab == null) return;
        tab.Disconnect();
        tab.Dispose();
        Tabs.Remove(tab);
        ConnectionCount = Tabs.Count;

        if (SelectedTab == tab)
            SelectedTab = Tabs.LastOrDefault();

        StatusMessage = "Ready";
    }

    [RelayCommand]
    private void AddFolder()
    {
        var folder = new ConnectionFolder { Name = "New Folder" };
        _storage.AddFolder(folder);
        LoadSessionTree();
    }

    public void SaveConnection(ConnectionInfo info)
    {
        var existing = _storage.Store.Connections.FindIndex(c => c.Id == info.Id);
        if (existing >= 0)
            _storage.UpdateConnection(info);
        else
            _storage.AddConnection(info);
        LoadSessionTree();
    }

    [RelayCommand]
    private void DeleteItem(SessionTreeItem? item)
    {
        if (item == null) return;
        if (item.IsFolder)
            _storage.DeleteFolder(item.Id);
        else
            _storage.DeleteConnection(item.Id);
        LoadSessionTree();
    }
}
