using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinSTerm.Models;
using WinSTerm.ViewModels;

namespace WinSTerm.Views;

public partial class SftpBrowserControl : UserControl
{
    private SftpBrowserViewModel ViewModel => (SftpBrowserViewModel)DataContext;

    public SftpBrowserControl()
    {
        InitializeComponent();
        DataContext = new SftpBrowserViewModel();
    }

    private void LocalFilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is SftpFileItem { IsDirectory: true } item)
        {
            ViewModel.NavigateLocalCommand.Execute(item.FullPath);
        }
    }

    private void RemoteFilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is SftpFileItem { IsDirectory: true } item)
        {
            ViewModel.NavigateRemoteCommand.Execute(item.FullPath);
        }
    }

    private void LocalFilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedLocalFiles = LocalFilesGrid.SelectedItems
            .Cast<SftpFileItem>().ToList();
    }

    private void RemoteFilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedRemoteFiles = RemoteFilesGrid.SelectedItems
            .Cast<SftpFileItem>().ToList();
    }

    private void LocalPathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.NavigateLocalCommand.Execute(ViewModel.LocalPath);
        }
    }

    private void RemotePathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.NavigateRemoteCommand.Execute(ViewModel.RemotePath);
        }
    }

    private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var inputBox = new Window
        {
            Title = "New Folder",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = FindResource("MahApps.Brushes.ThemeBackground") as System.Windows.Media.Brush,
            Owner = Window.GetWindow(this)
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        var label = new TextBlock
        {
            Text = "Enter folder name:",
            Foreground = FindResource("MahApps.Brushes.ThemeForeground") as System.Windows.Media.Brush,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var okBtn = new Button { Content = "Create", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };

        okBtn.Click += (_, _) => { inputBox.DialogResult = true; inputBox.Close(); };
        cancelBtn.Click += (_, _) => { inputBox.DialogResult = false; inputBox.Close(); };

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        stack.Children.Add(label);
        stack.Children.Add(textBox);
        stack.Children.Add(btnPanel);
        inputBox.Content = stack;

        if (inputBox.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            ViewModel.CreateRemoteFolderCommand.Execute(textBox.Text.Trim());
        }
    }
}
