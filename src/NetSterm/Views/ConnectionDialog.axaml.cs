using Avalonia.Controls;
using Avalonia.Interactivity;
using NetSterm.Models;
using NetSterm.ViewModels;

namespace NetSterm.Views;

public partial class ConnectionDialog : Window
{
    private readonly ConnectionDialogViewModel _viewModel;

    public ConnectionDialog() : this(null)
    {
    }

    public ConnectionDialog(ConnectionInfo? existing)
    {
        _viewModel = new ConnectionDialogViewModel(existing);
        DataContext = _viewModel;
        InitializeComponent();
    }

    public ConnectionInfo? Result => _viewModel.Result;

    private void PasswordRadio_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectedAuthMethod = AuthMethod.Password;
    }

    private void PrivateKeyRadio_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectedAuthMethod = AuthMethod.PrivateKey;
    }
}
