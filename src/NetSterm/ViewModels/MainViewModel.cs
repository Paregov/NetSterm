using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSterm.Models;
using NetSterm.Services;

namespace NetSterm.ViewModels;

public partial class MainViewModel : ObservableObject
{
    internal readonly ConnectionStorageService _storage = new();

    // Session tree (left sidebar)
    public ObservableCollection<SessionTreeItem> SessionTree { get; } = new();

    // Open tabs
    public ObservableCollection<SessionTabViewModel> Tabs { get; } = new();

    [ObservableProperty] private SessionTabViewModel? _selectedTab;

    // Home tab state: true when the Home tab is active (no session tab selected)
    [ObservableProperty] private bool _isHomeSelected = true;

    // SFTP sidebar
    public SftpSidebarViewModel SftpSidebar { get; } = new();

    // Snippets sidebar
    public SnippetsSidebarViewModel SnippetsSidebar { get; }

    // Active sidebar tab: "Sessions", "SFTP", or "Snippets"
    [ObservableProperty] private string _activeSidebar = "Sessions";

    public bool IsSessionsSidebarVisible => ActiveSidebar == "Sessions";
    public bool IsSftpSidebarVisible => ActiveSidebar == "SFTP";
    public bool IsSnippetsSidebarVisible => ActiveSidebar == "Snippets";

    // Quick connect fields
    [ObservableProperty] private string _quickHost = "";
    [ObservableProperty] private string _quickUsername = "";
    [ObservableProperty] private int _quickPort = 22;

    // Quick connect toolbar visibility
    [ObservableProperty] private bool _isQuickConnectVisible = SettingsService.Instance.Current.ShowQuickConnect;

    partial void OnIsQuickConnectVisibleChanged(bool value)
    {
        var settings = SettingsService.Instance.Current;
        settings.ShowQuickConnect = value;
        SettingsService.Instance.Save();
    }

    [RelayCommand]
    private void ToggleQuickConnect()
    {
        IsQuickConnectVisible = !IsQuickConnectVisible;
    }

    // Status bar
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private int _connectionCount;

    public MainViewModel()
    {
        SnippetsSidebar = new SnippetsSidebarViewModel();
        SnippetsSidebar.SnippetExecuteRequested += OnSnippetExecuteRequested;
        LoadSessionTree();
    }

    partial void OnActiveSidebarChanged(string value)
    {
        OnPropertyChanged(nameof(IsSessionsSidebarVisible));
        OnPropertyChanged(nameof(IsSftpSidebarVisible));
        OnPropertyChanged(nameof(IsSnippetsSidebarVisible));
    }

    public void SetActiveSidebar(string sidebar)
    {
        ActiveSidebar = sidebar;
    }

    private void OnSnippetExecuteRequested(string command)
    {
        if (SelectedTab?.SshService is { IsConnected: true } sshService)
        {
            sshService.SendData(command + "\n");
        }
    }

    partial void OnSelectedTabChanged(SessionTabViewModel? oldValue, SessionTabViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnSelectedTabPropertyChanged;
            oldValue.IsSelected = false;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnSelectedTabPropertyChanged;
            newValue.IsSelected = true;
            IsHomeSelected = false;
        }
        else
        {
            IsHomeSelected = true;
        }

        UpdateSftpSidebar();
    }

    private void OnSelectedTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionTabViewModel.IsConnected))
            UpdateSftpSidebar();
        else if (e.PropertyName == nameof(SessionTabViewModel.CurrentRemoteDirectory))
            OnTerminalCwdChanged();
    }

    private void OnTerminalCwdChanged()
    {
        if (SelectedTab == null || !SftpSidebar.IsConnected)
            return;
        var path = SelectedTab.CurrentRemoteDirectory;
        if (!string.IsNullOrEmpty(path) && path != SftpSidebar.CurrentPath)
        {
            _ = SftpSidebar.LoadDirectoryAsync(path);
        }
    }

    private void UpdateSftpSidebar()
    {
        if (SelectedTab?.IsConnected == true && SelectedTab.SftpService.IsConnected)
        {
            SftpSidebar.AttachToTab(SelectedTab);
            if (ActiveSidebar == "Sessions")
                ActiveSidebar = "SFTP";
        }
        else
        {
            SftpSidebar.AttachToTab(null);
            if (ActiveSidebar == "SFTP" && SelectedTab == null)
                ActiveSidebar = "Sessions";
        }
    }

    [RelayCommand]
    private void SelectHome()
    {
        SelectedTab = null;
    }

    public void LoadSessionTree()
    {
        var expandedIds = new HashSet<string>();
        CollectExpandedIds(SessionTree, expandedIds);

        SessionTree.Clear();
        var store = _storage.Store;

        var rootItems = new List<SessionTreeItem>();

        // Add folders
        var folderMap = new Dictionary<string, SessionTreeItem>();
        foreach (var folder in store.Folders.OrderBy(f => f.SortOrder))
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
        foreach (var folder in store.Folders.OrderBy(f => f.SortOrder))
        {
            if (folder.ParentFolderId != null && folderMap.TryGetValue(folder.ParentFolderId, out var parentItem))
                parentItem.Children.Add(folderMap[folder.Id]);
            else
                rootItems.Add(folderMap[folder.Id]);
        }

        // Add connections
        foreach (var conn in store.Connections.OrderBy(c => c.SortOrder))
        {
            var item = new SessionTreeItem
            {
                Id = conn.Id,
                Name = conn.Name,
                IsFolder = false,
                ConnectionInfo = conn
            };

            if (conn.FolderId != null && folderMap.TryGetValue(conn.FolderId, out var folderItem))
                folderItem.Children.Add(item);
            else
                rootItems.Add(item);
        }

        foreach (var item in rootItems)
            SessionTree.Add(item);

        RestoreExpandedIds(SessionTree, expandedIds);
    }

    private static void CollectExpandedIds(IEnumerable<SessionTreeItem> items, HashSet<string> ids)
    {
        foreach (var item in items)
        {
            if (item.IsFolder && item.IsExpanded)
                ids.Add(item.Id);
            CollectExpandedIds(item.Children, ids);
        }
    }

    private static void RestoreExpandedIds(IEnumerable<SessionTreeItem> items, HashSet<string> ids)
    {
        if (ids.Count == 0)
            return;
        foreach (var item in items)
        {
            if (item.IsFolder)
                item.IsExpanded = ids.Contains(item.Id);
            RestoreExpandedIds(item.Children, ids);
        }
    }

    [RelayCommand]
    private async Task QuickConnect()
    {
        if (string.IsNullOrWhiteSpace(QuickHost))
            return;

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
        if (item?.ConnectionInfo == null)
            return;
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
        if (tab == null)
            return;
        tab.Disconnect();
        tab.Dispose();
        Tabs.Remove(tab);
        ConnectionCount = Tabs.Count;

        if (SelectedTab == tab || SelectedTab == null)
            SelectedTab = Tabs.LastOrDefault();

        StatusMessage = "Ready";
    }

    public void CloseOtherTabs(SessionTabViewModel tab)
    {
        if (tab == null || !Tabs.Contains(tab))
            return;
        var others = Tabs.Where(t => t != tab).ToList();
        foreach (var t in others)
        {
            t.Disconnect();
            t.Dispose();
            Tabs.Remove(t);
        }
        SelectedTab = tab;
        ConnectionCount = Tabs.Count;
        StatusMessage = "Ready";
    }

    public void CloseAllTabs()
    {
        var all = Tabs.ToList();
        foreach (var t in all)
        {
            t.Disconnect();
            t.Dispose();
        }
        Tabs.Clear();
        SelectedTab = null;
        ConnectionCount = 0;
        StatusMessage = "Ready";
    }

    public void CloseTabsToRight(SessionTabViewModel tab)
    {
        if (tab == null)
            return;
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;

        var toClose = Tabs.Skip(index + 1).ToList();
        foreach (var t in toClose)
        {
            t.Disconnect();
            t.Dispose();
            Tabs.Remove(t);
        }
        ConnectionCount = Tabs.Count;
        if (SelectedTab == null || !Tabs.Contains(SelectedTab))
            SelectedTab = tab;
    }

    public void DuplicateTab(SessionTabViewModel tab)
    {
        if (tab == null)
            return;
        var newTab = new SessionTabViewModel(tab.ConnectionInfo);
        Tabs.Add(newTab);
        SelectedTab = newTab;
        ConnectionCount = Tabs.Count;
        StatusMessage = $"Duplicated tab for {tab.ConnectionInfo.Host}";
    }

    [RelayCommand]
    private void AddFolder()
    {
        var name = GetUniqueSessionName("New Folder", null);
        var folder = new ConnectionFolder { Name = name };
        _storage.AddFolder(folder);
        LoadSessionTree();
    }

    public void SaveConnection(ConnectionInfo info)
    {
        info.Name = GetUniqueSessionName(info.Name, info.FolderId, info.Id);

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
        if (item == null)
            return;
        if (item.IsFolder)
            _storage.DeleteFolder(item.Id);
        else
            _storage.DeleteConnection(item.Id);
        LoadSessionTree();
    }

    public void MoveSessionItem(SessionTreeItem item, string? newParentFolderId)
    {
        if (item.IsFolder)
        {
            var folder = _storage.Store.Folders.FirstOrDefault(f => f.Id == item.Id);
            if (folder == null)
                return;
            if (folder.ParentFolderId == newParentFolderId)
                return;
            folder.ParentFolderId = newParentFolderId;
            ReassignSortOrders(newParentFolderId);
            _storage.Save();
        }
        else if (item.ConnectionInfo != null)
        {
            var conn = _storage.Store.Connections.FirstOrDefault(c => c.Id == item.ConnectionInfo.Id);
            if (conn == null)
                return;
            if (conn.FolderId == newParentFolderId)
                return;
            conn.FolderId = newParentFolderId;
            ReassignSortOrders(newParentFolderId);
            _storage.Save();
        }
        LoadSessionTree();
    }

    private void ReassignSortOrders(string? parentFolderId)
    {
        int order = 0;
        foreach (var folder in _storage.Store.Folders
            .Where(f => f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.SortOrder))
        {
            folder.SortOrder = order++;
        }
        foreach (var conn in _storage.Store.Connections
            .Where(c => c.FolderId == parentFolderId)
            .OrderBy(c => c.SortOrder))
        {
            conn.SortOrder = order++;
        }
    }

    public void AddFolderWithInPlaceEdit(string? parentFolderId)
    {
        var name = GetUniqueSessionName("New Folder", parentFolderId);
        var folder = new ConnectionFolder { Name = name, ParentFolderId = parentFolderId };
        _storage.AddFolder(folder);
        LoadSessionTree();

        var newItem = FindTreeItem(folder.Id);
        if (newItem == null)
            return;

        newItem.IsEditing = true;
        newItem.IsSelected = true;

        if (parentFolderId != null)
        {
            var parent = FindTreeItem(parentFolderId);
            if (parent != null)
                parent.IsExpanded = true;
        }
    }

    public bool CommitRename(SessionTreeItem item)
    {
        var newName = item.Name.Trim();
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        string? folderId;
        string? excludeId;

        if (item.IsFolder)
        {
            var folder = _storage.Store.Folders.FirstOrDefault(f => f.Id == item.Id);
            if (folder == null)
                return false;
            folderId = folder.ParentFolderId;
            excludeId = folder.Id;
        }
        else
        {
            folderId = item.ConnectionInfo?.FolderId;
            excludeId = item.ConnectionInfo?.Id;
        }

        if (IsDuplicateSessionName(newName, folderId, excludeId))
        {
            return false;
        }

        if (item.IsFolder)
        {
            var folder = _storage.Store.Folders.First(f => f.Id == item.Id);
            folder.Name = newName;
        }
        else if (item.ConnectionInfo != null)
        {
            var conn = _storage.Store.Connections.FirstOrDefault(c => c.Id == item.ConnectionInfo.Id);
            if (conn != null)
                conn.Name = newName;
        }

        _storage.Save();
        return true;
    }

    public void CancelFolderRename(SessionTreeItem item)
    {
        LoadSessionTree();
    }

    private bool IsDuplicateSessionName(string name, string? folderId, string? excludeId = null)
    {
        var trimmedName = name.Trim();

        var folders = _storage.Store.Folders
            .Where(f => f.ParentFolderId == folderId && f.Id != excludeId);
        if (folders.Any(f => string.Equals(f.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            return true;

        var connections = _storage.Store.Connections
            .Where(c => c.FolderId == folderId && c.Id != excludeId);
        if (connections.Any(c => string.Equals(c.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private string GetUniqueSessionName(string baseName, string? folderId, string? excludeId = null)
    {
        var name = baseName.Trim();
        if (!IsDuplicateSessionName(name, folderId, excludeId))
            return name;

        for (int i = 2; i < 1000; i++)
        {
            var candidate = $"{name} ({i})";
            if (!IsDuplicateSessionName(candidate, folderId, excludeId))
                return candidate;
        }

        return name;
    }

    private SessionTreeItem? FindTreeItem(string id)
    {
        return FindInCollection(SessionTree, id);
    }

    private static SessionTreeItem? FindInCollection(IEnumerable<SessionTreeItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id)
                return item;
            var found = FindInCollection(item.Children, id);
            if (found != null)
                return found;
        }
        return null;
    }
}
