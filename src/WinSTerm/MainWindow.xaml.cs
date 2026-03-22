using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;
using WinSTerm.Models;
using WinSTerm.Services;
using WinSTerm.ViewModels;
using WinSTerm.Views;

namespace WinSTerm;

public partial class MainWindow : MetroWindow
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private async void SessionTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionTreeView.SelectedItem is SessionTreeItem item && !item.IsFolder)
        {
            try
            {
                await ConnectToSession(item.ConnectionInfo!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: false, ConnectionInfo: not null } item)
        {
            try
            {
                await ConnectToSession(item.ConnectionInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { ConnectionInfo: not null } item)
        {
            var dialog = new ConnectionDialog(item.ConnectionInfo) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                ViewModel.SaveConnection(dialog.Result);
            }
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { } item)
        {
            var result = MessageBox.Show(
                $"Delete '{item.Name}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                ViewModel.DeleteItemCommand.Execute(item);
        }
    }

    private void NewConnection_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionDialog() { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            ViewModel.SaveConnection(dialog.Result);
        }
    }

    private void NewConnectionFromTree_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionDialog() { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result != null)
            ViewModel.SaveConnection(dialog.Result);
    }

    private void NewFolderFromTree_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddFolderWithInPlaceEdit(null);
    }

    private void NewConnectionInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: true } folder)
        {
            var info = new ConnectionInfo { FolderId = folder.Id };
            var dialog = new ConnectionDialog(info) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                dialog.Result.FolderId = folder.Id;
                ViewModel.SaveConnection(dialog.Result);
            }
        }
    }

    private void NewSubfolderInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: true } folder)
        {
            ViewModel.AddFolderWithInPlaceEdit(folder.Id);
        }
    }

    private void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: true } item)
        {
            item.IsEditing = true;
        }
    }

    private void TreeItemTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void TreeItemTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: SessionTreeItem item } && item.IsEditing)
        {
            item.IsEditing = false;
            ViewModel.CommitFolderRename(item);
        }
    }

    private void TreeItemTextBox_KeyDown(object sender, KeyEventArgs e)
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

    private async Task ConnectToSession(ConnectionInfo info)
    {
        await ViewModel.OpenSession(info);

        var tab = ViewModel.SelectedTab;
        if (tab == null || tab.IsConnected) return;

        string? password = null;

        if (info.AuthMethod == AuthMethod.Password)
        {
            if (!string.IsNullOrEmpty(info.EncryptedPassword))
            {
                try { password = ConnectionStorageService.DecryptPassword(info.EncryptedPassword); }
                catch { password = null; }
            }

            if (password == null)
            {
                var pwdDialog = new PasswordDialog(info.Username, info.Host) { Owner = this };
                if (pwdDialog.ShowDialog() != true) return;
                password = pwdDialog.EnteredPassword;
            }
        }

        await tab.ConnectAsync(password);
    }

    private static SessionTreeItem? GetTreeItemFromMenuItem(object sender)
    {
        if (sender is MenuItem menuItem
            && menuItem.Parent is ContextMenu contextMenu
            && contextMenu.PlacementTarget is FrameworkElement fe
            && fe.DataContext is SessionTreeItem item)
            return item;
        return null;
    }

    private async void QuickConnect_Click(object sender, RoutedEventArgs e)
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
            MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        NewConnection_Click(sender, e);
    }

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromContextMenu(sender) is { } tab)
            ViewModel.CloseTabCommand.Execute(tab);
    }

    private void TabCloseOthers_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromContextMenu(sender) is { } tab)
            ViewModel.CloseOtherTabs(tab);
    }

    private void TabCloseAll_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseAllTabs();
    }

    private void TabCloseToRight_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromContextMenu(sender) is { } tab)
            ViewModel.CloseTabsToRight(tab);
    }

    private void TabDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromContextMenu(sender) is { } tab)
            ViewModel.DuplicateTab(tab);
    }

    private async void TabReconnect_Click(object sender, RoutedEventArgs e)
    {
        if (GetTabFromContextMenu(sender) is not { } tab) return;
        if (tab.IsConnected) return;

        try
        {
            string? password = null;
            var info = tab.ConnectionInfo;

            if (info.AuthMethod == AuthMethod.Password)
            {
                if (!string.IsNullOrEmpty(info.EncryptedPassword))
                {
                    try { password = ConnectionStorageService.DecryptPassword(info.EncryptedPassword); }
                    catch { password = null; }
                }

                if (password == null)
                {
                    var pwdDialog = new PasswordDialog(info.Username, info.Host) { Owner = this };
                    if (pwdDialog.ShowDialog() != true) return;
                    password = pwdDialog.EnteredPassword;
                }
            }

            await tab.ConnectAsync(password);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Reconnect Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static SessionTabViewModel? GetTabFromContextMenu(object sender)
    {
        if (sender is MenuItem menuItem
            && menuItem.Parent is ContextMenu contextMenu
            && contextMenu.PlacementTarget is FrameworkElement fe)
            return fe.DataContext as SessionTabViewModel;
        return null;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { Owner = this };
        dialog.ShowDialog();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.OemComma && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Settings_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}