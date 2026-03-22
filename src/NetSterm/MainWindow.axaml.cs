using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Serilog;
using NetSterm.Models;
using NetSterm.Services;
using NetSterm.ViewModels;
using NetSterm.Views;

namespace NetSterm;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private MainViewModel ViewModel => _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Log.Information("MainWindow created");
    }

    // ===== Session Tree Event Handlers =====

    private async void SessionTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SessionTreeView.SelectedItem is SessionTreeItem item && !item.IsFolder)
        {
            Log.Debug("Session tree item double-tapped: {Name}", item.Name);
            try
            {
                await ConnectToSession(item.ConnectionInfo!);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Connection error after double-tap on {Name}", item.Name);
                await ShowErrorAsync("Connection Error", ex.Message);
            }
        }
    }

    private async void ConnectMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: false, ConnectionInfo: not null } item)
        {
            try
            {
                await ConnectToSession(item.ConnectionInfo);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Connection Error", ex.Message);
            }
        }
    }

    private async void EditMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { ConnectionInfo: not null } item)
        {
            var dialog = new ConnectionDialog(item.ConnectionInfo);
            await dialog.ShowDialog(this);
            if (dialog.Result != null)
            {
                ViewModel.SaveConnection(dialog.Result);
            }
        }
    }

    private async void DeleteMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { } item)
        {
            var confirmed = await ShowConfirmAsync("Confirm Delete", $"Delete '{item.Name}'?");
            if (confirmed)
                ViewModel.DeleteItemCommand.Execute(item);
        }
    }

    private async void SessionTreeView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (SessionTreeView.SelectedItem is not SessionTreeItem item) return;

        if (e.Key == Key.F2)
        {
            item.IsEditing = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            var confirmed = await ShowConfirmAsync("Confirm Delete", $"Delete '{item.Name}'?");
            if (confirmed)
                ViewModel.DeleteItemCommand.Execute(item);
            e.Handled = true;
        }
    }

    private async void NewConnection_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result != null)
        {
            ViewModel.SaveConnection(dialog.Result);
        }
    }

    private async void NewConnectionFromTree_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result != null)
            ViewModel.SaveConnection(dialog.Result);
    }

    private void NewFolderFromTree_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFolderWithInPlaceEdit(null);
    }

    // ===== Sessions Toolbar Handlers =====

    private void AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        var parentFolderId = GetSelectedFolderId();
        ViewModel.AddFolderWithInPlaceEdit(parentFolderId);
    }

    private async void AddConnection_Click(object? sender, RoutedEventArgs e)
    {
        var parentFolderId = GetSelectedFolderId();
        var dialog = new ConnectionDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result != null)
        {
            if (parentFolderId != null)
                dialog.Result.FolderId = parentFolderId;
            ViewModel.SaveConnection(dialog.Result);
        }
    }

    /// <summary>
    /// Gets the folder ID based on current session tree selection.
    /// If a folder is selected, returns its ID.
    /// If a connection is selected, returns its parent folder ID.
    /// If nothing is selected, returns null (root level).
    /// </summary>
    private string? GetSelectedFolderId()
    {
        if (SessionTreeView.SelectedItem is SessionTreeItem item)
        {
            if (item.IsFolder)
                return item.Id;
            return item.ConnectionInfo?.FolderId;
        }
        return null;
    }

    private async void NewConnectionInFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: true } folder)
        {
            var dialog = new ConnectionDialog();
            await dialog.ShowDialog(this);
            if (dialog.Result != null)
            {
                dialog.Result.FolderId = folder.Id;
                ViewModel.SaveConnection(dialog.Result);
            }
        }
    }

    private void NewSubfolderInFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: true } folder)
        {
            ViewModel.AddFolderWithInPlaceEdit(folder.Id);
        }
    }

    private void RenameItem_Click(object? sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { } item)
        {
            item.IsEditing = true;
        }
    }

    private void TreeItemTextBox_Attached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void TreeItemTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: SessionTreeItem item } && item.IsEditing)
        {
            if (!ViewModel.CommitRename(item))
            {
                ViewModel.CancelFolderRename(item);
            }
            item.IsEditing = false;
        }
    }

    private void TreeItemTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: SessionTreeItem item } tb) return;

        if (e.Key == Key.Enter)
        {
            if (ViewModel.CommitRename(item))
            {
                item.IsEditing = false;
            }
            else
            {
                ToolTip.SetTip(tb, "This name is already used at this level");
                ToolTip.SetIsOpen(tb, true);
                tb.SelectAll();

                var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, _) => { ToolTip.SetIsOpen(tb, false); timer.Stop(); };
                timer.Start();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            item.IsEditing = false;
            ViewModel.CancelFolderRename(item);
            e.Handled = true;
        }
    }

    // ===== Connection Logic =====

    private async Task ConnectToSession(ConnectionInfo info)
    {
        Log.Information("Connecting to session {Host}:{Port}", info.Host, info.Port);
        await ViewModel.OpenSession(info);

        var tab = ViewModel.SelectedTab;
        if (tab == null || tab.IsConnected) return;

        string? password = null;

        if (info.AuthMethod == AuthMethod.Password && !string.IsNullOrEmpty(info.EncryptedPassword))
        {
            try { password = ConnectionStorageService.DecryptPassword(info.EncryptedPassword); }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to decrypt password for {Host}", info.Host);
                password = null;
            }
        }

        try
        {
            await tab.ConnectAsync(password);
            ViewModel.StatusMessage = $"Connected to {info.Host}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to {Host}:{Port}", info.Host, info.Port);
            ViewModel.StatusMessage = $"Connection failed: {info.Host}";
            await ShowErrorAsync("Connection Error",
                $"Failed to connect to {info.Host}:{info.Port}\n\n{ex.Message}");
            ViewModel.CloseTabCommand.Execute(tab);
        }
    }

    private static SessionTreeItem? GetTreeItemFromMenuItem(object? sender)
    {
        // In Avalonia, context menu items inherit the DataContext of the target element
        if (sender is MenuItem { DataContext: SessionTreeItem item })
            return item;
        return null;
    }

    // ===== Quick Connect =====

    private async void QuickConnect_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.QuickHost)) return;

        var info = new ConnectionInfo
        {
            Name = $"{ViewModel.QuickUsername}@{ViewModel.QuickHost}",
            Host = ViewModel.QuickHost,
            Port = ViewModel.QuickPort,
            Username = string.IsNullOrWhiteSpace(ViewModel.QuickUsername) ? "root" : ViewModel.QuickUsername,
            AuthMethod = AuthMethod.Password
        };

        try
        {
            await ConnectToSession(info);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Connection Error", ex.Message);
        }
    }

    // ===== Tab Management =====

    private void NewTabButton_Click(object? sender, RoutedEventArgs e)
    {
        NewConnection_Click(sender, e);
    }

    private void HomeTab_Click(object? sender, PointerPressedEventArgs e)
    {
        ViewModel.SelectHomeCommand.Execute(null);
    }

    private void TabCloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SessionTabViewModel tab })
            ViewModel.CloseTabCommand.Execute(tab);
    }

    private void TabClose_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SessionTabViewModel tab })
            ViewModel.CloseTabCommand.Execute(tab);
    }

    private void TabCloseOthers_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SessionTabViewModel tab })
            ViewModel.CloseOtherTabs(tab);
    }

    private void TabCloseAll_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.CloseAllTabs();
    }

    private void TabCloseToRight_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SessionTabViewModel tab })
            ViewModel.CloseTabsToRight(tab);
    }

    private void TabDuplicate_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SessionTabViewModel tab })
            ViewModel.DuplicateTab(tab);
    }

    private async void TabReconnect_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SessionTabViewModel tab }) return;
        if (tab.IsConnected) return;

        try
        {
            string? password = null;
            var info = tab.ConnectionInfo;

            if (info.AuthMethod == AuthMethod.Password && !string.IsNullOrEmpty(info.EncryptedPassword))
            {
                try { password = ConnectionStorageService.DecryptPassword(info.EncryptedPassword); }
                catch (Exception decryptEx)
                {
                    Log.Warning(decryptEx, "Failed to decrypt password for reconnect to {Host}", info.Host);
                    password = null;
                }
            }

            await tab.ConnectAsync(password);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reconnect failed for {Host}", tab.ConnectionInfo.Host);
            await ShowErrorAsync("Reconnect Error", ex.Message);
        }
    }

    // ===== Menu Handlers =====

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog();
        await dialog.ShowDialog(this);
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }

    private async void ExportConfig_Click(object? sender, RoutedEventArgs e)
    {
        Log.Information("Exporting configuration");
        var exportDialog = new ExportDialog();
        await exportDialog.ShowDialog(this);
        if (exportDialog.Result == null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Configuration",
            DefaultExtension = ".zip",
            SuggestedFileName = $"NetSterm-export-{DateTime.Now:yyyyMMdd}",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("NetSterm Package") { Patterns = new[] { "*.zip" } }
            }
        });

        if (file == null) return;

        try
        {
            var service = new ConfigurationExportService(
                new ConnectionStorageService(), SnippetStorageService.Instance);
            service.Export(file.Path.LocalPath, exportDialog.Result);

            await ShowInfoAsync("Export Complete",
                $"Configuration exported successfully to:\n{file.Path.LocalPath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Configuration export failed");
            await ShowErrorAsync("Export Error", $"Export failed:\n{ex.Message}");
        }
    }

    private async void ImportConfig_Click(object? sender, RoutedEventArgs e)
    {
        Log.Information("Importing configuration");
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Configuration",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("NetSterm Package") { Patterns = new[] { "*.zip" } }
            }
        });

        if (files.Count == 0) return;

        try
        {
            var service = new ConfigurationExportService(
                ViewModel._storage, SnippetStorageService.Instance);
            var result = service.Import(files[0].Path.LocalPath);

            ViewModel.LoadSessionTree();
            ViewModel.SnippetsSidebar.LoadTree();

            var summary = new List<string>();
            if (result.ConnectionsAdded > 0)
                summary.Add($"{result.ConnectionsAdded} connection(s)");
            if (result.ConnectionFoldersAdded > 0)
                summary.Add($"{result.ConnectionFoldersAdded} connection folder(s)");
            if (result.SnippetsAdded > 0)
                summary.Add($"{result.SnippetsAdded} snippet(s)");
            if (result.SnippetFoldersAdded > 0)
                summary.Add($"{result.SnippetFoldersAdded} snippet folder(s)");
            if (result.PrivateKeysImported > 0)
                summary.Add($"{result.PrivateKeysImported} private key(s)");

            var msg = summary.Count > 0
                ? $"Imported:\n{string.Join("\n", summary)}"
                : "No new items to import (all duplicates).";

            await ShowInfoAsync("Import Complete", msg);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Configuration import failed");
            await ShowErrorAsync("Import Error", $"Import failed:\n{ex.Message}");
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.OemComma && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Settings_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ===== SFTP Sidebar Event Handlers =====

    private async void SftpTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SftpTreeView.SelectedItem is SftpTreeNode { IsDirectory: false } node)
        {
            try
            {
                if (ViewModel.SftpSidebar == null) return;
                await ViewModel.SftpSidebar.DownloadAndOpenAsync(node);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Download Error", ex.Message);
            }
        }
    }

    private async void SftpDownload_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSftpNodeFromMenuItem(sender) is { IsDirectory: false } node)
        {
            try
            {
                await ViewModel.SftpSidebar.DownloadAndOpenAsync(node);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Download Error", ex.Message);
            }
        }
    }

    private async void SftpDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSftpNodeFromMenuItem(sender) is { } node)
        {
            var confirmed = await ShowConfirmAsync("Confirm Delete", $"Delete '{node.Name}'?");
            if (!confirmed) return;

            try
            {
                await ViewModel.SftpSidebar.DeleteNodeAsync(node);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Delete Error", ex.Message);
            }
        }
    }

    private async void SftpRename_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSftpNodeFromMenuItem(sender) is { } node)
        {
            var newName = await ShowInputAsync("Rename", $"Enter new name for '{node.Name}':", node.Name);

            if (string.IsNullOrWhiteSpace(newName) || newName == node.Name) return;

            try
            {
                await ViewModel.SftpSidebar.RenameNodeAsync(node, newName);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Rename Error", ex.Message);
            }
        }
    }

    private async void SftpNewFolder_Click(object? sender, RoutedEventArgs e)
    {
        var parentNode = GetSftpNodeFromMenuItem(sender);
        if (parentNode is { IsDirectory: false })
            parentNode = null;

        var folderName = await ShowInputAsync("New Folder", "Enter folder name:", "New Folder");
        if (string.IsNullOrWhiteSpace(folderName)) return;

        try
        {
            await ViewModel.SftpSidebar.CreateFolderAsync(parentNode, folderName);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Create Folder Error", ex.Message);
        }
    }

    private void SftpTreeView_DragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // Using Data for compatibility; DataTransfer API migration pending
        if (e.Data.Contains(DataFormats.Files) && ViewModel.SftpSidebar.IsConnected)
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
#pragma warning restore CS0618
        e.Handled = true;
    }

    private async void SftpTreeView_Drop(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // Using Data for compatibility; DataTransfer API migration pending
        if (!e.Data.Contains(DataFormats.Files)) return;
        if (!ViewModel.SftpSidebar.IsConnected) return;

        var storageItems = e.Data.GetFiles();
#pragma warning restore CS0618
        if (storageItems == null) return;

        var files = storageItems
            .Select(item => item.Path.LocalPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToArray();
        if (files.Length == 0) return;

        string targetPath = ViewModel.SftpSidebar.CurrentPath ?? "/";

        if (e.Source is Visual source)
        {
            var treeViewItem = source.FindAncestorOfType<TreeViewItem>();
            if (treeViewItem?.DataContext is SftpTreeNode { IsDirectory: true } folderNode)
            {
                targetPath = folderNode.FullPath;
            }
        }

        try
        {
            await ViewModel.SftpSidebar.UploadFilesAsync(files, targetPath);
            ViewModel.StatusMessage = $"Uploaded {files.Length} file(s) to {targetPath}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Upload Error", $"Upload failed:\n{ex.Message}");
        }
    }

    private async void SftpUploadHere_Click(object? sender, RoutedEventArgs e)
    {
        var targetPath = ViewModel.SftpSidebar.CurrentPath ?? "/";
        if (GetSftpNodeFromMenuItem(sender) is { IsDirectory: true } node)
            targetPath = node.FullPath;

        var storageFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select files to upload",
            AllowMultiple = true
        });

        if (storageFiles.Count == 0) return;

        var filePaths = storageFiles.Select(f => f.Path.LocalPath).ToArray();

        try
        {
            await ViewModel.SftpSidebar.UploadFilesAsync(filePaths, targetPath);
            ViewModel.StatusMessage = $"Uploaded {filePaths.Length} file(s) to {targetPath}";
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Upload Error", $"Upload failed:\n{ex.Message}");
        }
    }

    private static SftpTreeNode? GetSftpNodeFromMenuItem(object? sender)
    {
        if (sender is MenuItem { DataContext: SftpTreeNode node })
            return node;
        return null;
    }

    // ===== Session Tree Drag-and-Drop =====

#pragma warning disable CS0618 // Using legacy DragDrop API for compatibility
    private async void SessionTreeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: SessionTreeItem item } control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        var dragData = new DataObject();
        dragData.Set("SessionTreeItem", item);
        await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
    }

    private void SessionTree_DragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("SessionTreeItem"))
        {
            e.DragEffects = DragDropEffects.None;
        }
        else
        {
            e.DragEffects = DragDropEffects.Move;
        }
        e.Handled = true;
    }

    private void SessionTree_Drop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("SessionTreeItem")) return;
        var draggedItem = e.Data.Get("SessionTreeItem") as SessionTreeItem;
        if (draggedItem == null) return;

        var targetItem = FindTargetTreeItem<SessionTreeItem>(e);

        if (targetItem == draggedItem) return;
        if (draggedItem.IsFolder && IsSessionDescendant(draggedItem, targetItem)) return;

        string? newParentFolderId = null;
        if (targetItem != null)
        {
            if (targetItem.IsFolder)
                newParentFolderId = targetItem.Id;
            else
                newParentFolderId = targetItem.ConnectionInfo?.FolderId;
        }

        ViewModel.MoveSessionItem(draggedItem, newParentFolderId);
        e.Handled = true;
    }

    // ===== Snippet Tree Drag-and-Drop =====

    private async void SnippetTreeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: SnippetTreeItem item } control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        var dragData = new DataObject();
        dragData.Set("SnippetTreeItem", item);
        await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
    }

    private void SnippetTree_DragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("SnippetTreeItem"))
        {
            e.DragEffects = DragDropEffects.None;
        }
        else
        {
            e.DragEffects = DragDropEffects.Move;
        }
        e.Handled = true;
    }

    private void SnippetTree_Drop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("SnippetTreeItem")) return;
        var draggedItem = e.Data.Get("SnippetTreeItem") as SnippetTreeItem;
        if (draggedItem == null) return;

        var targetItem = FindTargetTreeItem<SnippetTreeItem>(e);

        if (targetItem == draggedItem) return;
        if (draggedItem.IsFolder && IsSnippetDescendant(draggedItem, targetItem)) return;

        string? newParentFolderId = null;
        if (targetItem != null)
        {
            if (targetItem.IsFolder)
                newParentFolderId = targetItem.Id;
            else
                newParentFolderId = targetItem.Snippet?.FolderId;
        }

        ViewModel.SnippetsSidebar.MoveSnippetItem(draggedItem, newParentFolderId);
        e.Handled = true;
    }
#pragma warning restore CS0618

    // ===== Drag-and-Drop Helpers =====

    private static T? FindTargetTreeItem<T>(DragEventArgs e) where T : class
    {
        if (e.Source is not Control source) return null;

        var current = source as Visual;
        while (current != null)
        {
            if (current is Control { DataContext: T item })
                return item;
            current = current.GetVisualParent();
        }
        return null;
    }

    private static bool IsSessionDescendant(SessionTreeItem parent, SessionTreeItem? target)
    {
        if (target == null) return false;
        foreach (var child in parent.Children)
        {
            if (child == target) return true;
            if (IsSessionDescendant(child, target)) return true;
        }
        return false;
    }

    private static bool IsSnippetDescendant(SnippetTreeItem parent, SnippetTreeItem? target)
    {
        if (target == null) return false;
        foreach (var child in parent.Children)
        {
            if (child == target) return true;
            if (IsSnippetDescendant(child, target)) return true;
        }
        return false;
    }

    // ===== Sidebar Tab Switching =====

    private void SessionsTab_Checked(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetActiveSidebar("Sessions");
    }

    private void SftpTab_Checked(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetActiveSidebar("SFTP");
    }

    private void SnippetsTab_Checked(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetActiveSidebar("Snippets");
    }

    // ===== Snippets Sidebar Event Handlers =====

    /// <summary>
    /// Gets the folder ID based on current snippet tree selection.
    /// If a folder is selected, returns its ID.
    /// If a snippet is selected, returns its parent folder ID.
    /// If nothing is selected, returns null (root level).
    /// </summary>
    private string? GetSelectedSnippetFolderId()
    {
        if (SnippetTreeView.SelectedItem is SnippetTreeItem item)
        {
            if (item.IsFolder)
                return item.Id;
            return item.Snippet?.FolderId;
        }
        return null;
    }

    private async void AddSnippet_Click(object? sender, RoutedEventArgs e)
    {
        var parentFolderId = GetSelectedSnippetFolderId();
        var dialog = new SnippetEditDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result != null)
        {
            ViewModel.SnippetsSidebar.AddSnippet(dialog.Result, parentFolderId);
        }
    }

    private void AddSnippetFolder_Click(object? sender, RoutedEventArgs e)
    {
        var parentFolderId = GetSelectedSnippetFolderId();
        ViewModel.SnippetsSidebar.AddFolderWithInPlaceEdit(parentFolderId);
    }

    private async void AddSnippetFromTree_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SnippetEditDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result != null)
        {
            ViewModel.SnippetsSidebar.AddSnippet(dialog.Result);
        }
    }

    private void AddSnippetFolderFromTree_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SnippetsSidebar.AddFolderWithInPlaceEdit(null);
    }

    private async void AddSnippetInFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSnippetTreeItemFromMenuItem(sender) is { IsFolder: true } folder)
        {
            var dialog = new SnippetEditDialog();
            await dialog.ShowDialog(this);
            if (dialog.Result != null)
            {
                ViewModel.SnippetsSidebar.AddSnippet(dialog.Result, folder.Id);
            }
        }
    }

    private void AddSnippetSubfolder_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSnippetTreeItemFromMenuItem(sender) is { IsFolder: true } folder)
        {
            ViewModel.SnippetsSidebar.AddFolderWithInPlaceEdit(folder.Id);
        }
    }

    private void RenameSnippetFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSnippetTreeItemFromMenuItem(sender) is { } item)
        {
            item.IsEditing = true;
        }
    }

    private async void EditSnippetTree_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSnippetTreeItemFromMenuItem(sender) is { IsFolder: false, Snippet: not null } item)
        {
            var dialog = new SnippetEditDialog(item.Snippet);
            await dialog.ShowDialog(this);
            if (dialog.Result != null)
            {
                dialog.Result.FolderId = item.Snippet.FolderId;
                ViewModel.SnippetsSidebar.UpdateSnippet(dialog.Result);
            }
        }
    }

    private async void DeleteSnippetTree_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSnippetTreeItemFromMenuItem(sender) is not { } item) return;

        var confirmed = await ShowConfirmAsync("Confirm Delete", $"Delete '{item.Name}'?");
        if (confirmed)
            ViewModel.SnippetsSidebar.DeleteItem(item);
    }

    private async void SnippetTreeView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (SnippetTreeView.SelectedItem is not SnippetTreeItem item) return;

        if (e.Key == Key.F2)
        {
            item.IsEditing = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            var confirmed = await ShowConfirmAsync("Confirm Delete", $"Delete '{item.Name}'?");
            if (confirmed)
                ViewModel.SnippetsSidebar.DeleteItem(item);
            e.Handled = true;
        }
    }

    private void ExecuteSnippetTree_Click(object? sender, RoutedEventArgs e)
    {
        if (GetSnippetTreeItemFromMenuItem(sender) is { IsFolder: false, Snippet: not null } item)
            ViewModel.SnippetsSidebar.ExecuteSnippet(item.Snippet);
    }

    private void SnippetTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SnippetTreeView.SelectedItem is SnippetTreeItem { IsFolder: false, Snippet: not null } item)
        {
            ViewModel.SnippetsSidebar.ExecuteSnippet(item.Snippet);
        }
    }

    private void SnippetFolderTextBox_Attached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void SnippetFolderTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: SnippetTreeItem item } && item.IsEditing)
        {
            if (!ViewModel.SnippetsSidebar.CommitRename(item))
            {
                ViewModel.SnippetsSidebar.CancelFolderRename();
            }
            item.IsEditing = false;
        }
    }

    private void SnippetFolderTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: SnippetTreeItem item } tb) return;

        if (e.Key == Key.Enter)
        {
            if (ViewModel.SnippetsSidebar.CommitRename(item))
            {
                item.IsEditing = false;
            }
            else
            {
                ToolTip.SetTip(tb, "This name is already used at this level");
                ToolTip.SetIsOpen(tb, true);
                tb.SelectAll();

                var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, _) => { ToolTip.SetIsOpen(tb, false); timer.Stop(); };
                timer.Start();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            item.IsEditing = false;
            ViewModel.SnippetsSidebar.CancelFolderRename();
            e.Handled = true;
        }
    }

    private static SnippetTreeItem? GetSnippetTreeItemFromMenuItem(object? sender)
    {
        if (sender is MenuItem { DataContext: SnippetTreeItem item })
            return item;
        return null;
    }

    // ===== Dialog Helpers =====

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SizeToContent = SizeToContent.Height
        };

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(24, 6),
            IsDefault = true
        };
        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 380
                },
                okButton
            }
        };

        await dialog.ShowDialog(this);
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        await ShowErrorAsync(title, message);
    }

    private async Task<bool> ShowConfirmAsync(string title, string message)
    {
        bool result = false;

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SizeToContent = SizeToContent.Height
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Padding = new Thickness(24, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        yesButton.Click += (_, _) => { result = true; dialog.Close(); };

        var noButton = new Button
        {
            Content = "No",
            Padding = new Thickness(24, 6),
            IsDefault = true
        };
        noButton.Click += (_, _) => dialog.Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { yesButton, noButton }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 340
                },
                buttonPanel
            }
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<string?> ShowInputAsync(string title, string prompt, string defaultText = "")
    {
        string? result = null;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SizeToContent = SizeToContent.Height
        };

        var inputBox = new TextBox
        {
            Text = defaultText,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var okButton = new Button
        {
            Content = "OK",
            Padding = new Thickness(24, 6),
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okButton.Click += (_, _) => { result = inputBox.Text; dialog.Close(); };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(24, 6)
        };
        cancelButton.Click += (_, _) => dialog.Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { okButton, cancelButton }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Text = prompt,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 360
                },
                inputBox,
                buttonPanel
            }
        };

        await dialog.ShowDialog(this);
        return result;
    }
}
