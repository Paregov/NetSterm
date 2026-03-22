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
using WinSTerm.Models;
using WinSTerm.Services;
using WinSTerm.ViewModels;
using WinSTerm.Views;

namespace WinSTerm;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private MainViewModel ViewModel => _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    // ===== Session Tree Event Handlers =====

    private async void SessionTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SessionTreeView.SelectedItem is SessionTreeItem item && !item.IsFolder)
        {
            try
            {
                await ConnectToSession(item.ConnectionInfo!);
            }
            catch (Exception ex)
            {
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
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: true } item)
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
            item.IsEditing = false;
            ViewModel.CommitFolderRename(item);
        }
    }

    private void TreeItemTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: SessionTreeItem item }) return;

        if (e.Key == Key.Enter)
        {
            item.IsEditing = false;
            ViewModel.CommitFolderRename(item);
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
        await ViewModel.OpenSession(info);

        var tab = ViewModel.SelectedTab;
        if (tab == null || tab.IsConnected) return;

        string? password = null;

        if (info.AuthMethod == AuthMethod.Password && !string.IsNullOrEmpty(info.EncryptedPassword))
        {
            try { password = ConnectionStorageService.DecryptPassword(info.EncryptedPassword); }
            catch { password = null; }
        }

        try
        {
            await tab.ConnectAsync(password);
        }
        catch (Exception ex)
        {
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
                catch { password = null; }
            }

            await tab.ConnectAsync(password);
        }
        catch (Exception ex)
        {
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
        var exportDialog = new ExportDialog();
        await exportDialog.ShowDialog(this);
        if (exportDialog.Result == null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Configuration",
            DefaultExtension = ".zip",
            SuggestedFileName = $"winsterm-export-{DateTime.Now:yyyyMMdd}",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("WinSTerm Package") { Patterns = new[] { "*.zip" } }
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
            await ShowErrorAsync("Export Error", $"Export failed:\n{ex.Message}");
        }
    }

    private async void ImportConfig_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Configuration",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("WinSTerm Package") { Patterns = new[] { "*.zip" } }
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

    private async void AddSnippet_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SnippetEditDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result != null)
        {
            ViewModel.SnippetsSidebar.AddSnippet(dialog.Result);
        }
    }

    private void AddSnippetFolder_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.SnippetsSidebar.AddFolderWithInPlaceEdit(null);
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
        if (GetSnippetTreeItemFromMenuItem(sender) is { IsFolder: true } item)
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
            item.IsEditing = false;
            ViewModel.SnippetsSidebar.CommitFolderRename(item);
        }
    }

    private void SnippetFolderTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: SnippetTreeItem item }) return;

        if (e.Key == Key.Enter)
        {
            item.IsEditing = false;
            ViewModel.SnippetsSidebar.CommitFolderRename(item);
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
