using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

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
            oldValue.PropertyChanged -= OnSelectedTabPropertyChanged;

        if (newValue != null)
        {
            newValue.PropertyChanged += OnSelectedTabPropertyChanged;
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
        if (SelectedTab == null || !SftpSidebar.IsConnected) return;
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

        if (SelectedTab == tab || SelectedTab == null)
            SelectedTab = Tabs.LastOrDefault();

        StatusMessage = "Ready";
    }

    public void CloseOtherTabs(SessionTabViewModel tab)
    {
        if (tab == null || !Tabs.Contains(tab)) return;
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
        if (tab == null) return;
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

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
        if (tab == null) return;
        var newTab = new SessionTabViewModel(tab.ConnectionInfo);
        Tabs.Add(newTab);
        SelectedTab = newTab;
        ConnectionCount = Tabs.Count;
        StatusMessage = $"Duplicated tab for {tab.ConnectionInfo.Host}";
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

    public void AddFolderWithInPlaceEdit(string? parentFolderId)
    {
        var folder = new ConnectionFolder { Name = "New Folder", ParentFolderId = parentFolderId };
        _storage.AddFolder(folder);
        LoadSessionTree();

        var newItem = FindTreeItem(folder.Id);
        if (newItem == null) return;

        newItem.IsEditing = true;
        newItem.IsSelected = true;

        if (parentFolderId != null)
        {
            var parent = FindTreeItem(parentFolderId);
            if (parent != null) parent.IsExpanded = true;
        }
    }

    public void CommitFolderRename(SessionTreeItem item)
    {
        if (!item.IsFolder || string.IsNullOrWhiteSpace(item.Name)) return;

        var folder = _storage.Store.Folders.FirstOrDefault(f => f.Id == item.Id);
        if (folder == null) return;

        folder.Name = item.Name.Trim();
        _storage.Save();
    }

    public void CancelFolderRename(SessionTreeItem item)
    {
        LoadSessionTree();
    }

    private SessionTreeItem? FindTreeItem(string id)
    {
        return FindInCollection(SessionTree, id);
    }

    private static SessionTreeItem? FindInCollection(IEnumerable<SessionTreeItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id) return item;
            var found = FindInCollection(item.Children, id);
            if (found != null) return found;
        }
        return null;
    }
}
