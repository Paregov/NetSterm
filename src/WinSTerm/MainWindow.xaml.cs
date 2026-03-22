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

    private void SessionTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionTreeView.SelectedItem is SessionTreeItem item && !item.IsFolder)
        {
            _ = ConnectToSession(item.ConnectionInfo!);
        }
    }

    private void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetTreeItemFromMenuItem(sender) is { IsFolder: false, ConnectionInfo: not null } item)
            _ = ConnectToSession(item.ConnectionInfo);
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

        // Terminal and SFTP controls are wired via DataTemplate
        // We need to find them after rendering
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        AttachTerminalAndSftp(tab);
    }

    private void AttachTerminalAndSftp(SessionTabViewModel tab)
    {
        // Find the TerminalControl inside the current tab's content
        var tabControl = FindVisualChild<TabControl>(this);
        if (tabControl == null) return;

        var container = tabControl.ItemContainerGenerator.ContainerFromItem(tab) as TabItem;
        if (container == null) return;

        var terminalControl = FindVisualChild<TerminalControl>(container);
        if (terminalControl != null)
        {
            terminalControl.AttachSshService(tab.SshService);
        }

        var sftpControl = FindVisualChild<SftpBrowserControl>(container);
        if (sftpControl != null && sftpControl.DataContext is SftpBrowserViewModel sftpVm)
        {
            sftpVm.AttachService(tab.SftpService);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
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

        await ConnectToSession(info);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}