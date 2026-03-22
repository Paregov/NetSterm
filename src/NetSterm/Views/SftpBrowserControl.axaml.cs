using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using NetSterm.Models;
using NetSterm.ViewModels;

namespace NetSterm.Views;

public partial class SftpBrowserControl : UserControl
{
    private SftpBrowserViewModel ViewModel => (SftpBrowserViewModel)DataContext!;

    public SftpBrowserControl()
    {
        InitializeComponent();
    }

    private void LocalFilesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is SftpFileItem { IsDirectory: true } item)
        {
            ViewModel.NavigateLocalCommand.Execute(item.FullPath);
        }
    }

    private void RemoteFilesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is SftpFileItem { IsDirectory: true } item)
        {
            ViewModel.NavigateRemoteCommand.Execute(item.FullPath);
        }
    }

    private void LocalFilesGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedLocalFiles = LocalFilesGrid.SelectedItems
            .Cast<SftpFileItem>().ToList();
    }

    private void RemoteFilesGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedRemoteFiles = RemoteFilesGrid.SelectedItems
            .Cast<SftpFileItem>().ToList();
    }

    private void LocalPathBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.NavigateLocalCommand.Execute(ViewModel.LocalPath);
        }
    }

    private void RemotePathBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.NavigateRemoteCommand.Execute(ViewModel.RemotePath);
        }
    }

    private async void CreateFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var parent = TopLevel.GetTopLevel(this) as Window;
        if (parent == null) return;

        var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };

        var dialog = new Window
        {
            Title = "New Folder",
            Width = 350,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var okBtn = new Button
        {
            Content = "Create",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var cancelBtn = new Button { Content = "Cancel", Width = 80 };

        string? folderName = null;
        okBtn.Click += (_, _) =>
        {
            folderName = textBox.Text;
            dialog.Close();
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text = "Enter folder name:",
                    Margin = new Thickness(0, 0, 0, 8)
                },
                textBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { okBtn, cancelBtn }
                }
            }
        };

        await dialog.ShowDialog(parent);

        if (!string.IsNullOrWhiteSpace(folderName))
        {
            ViewModel.CreateRemoteFolderCommand.Execute(folderName.Trim());
        }
    }
}
