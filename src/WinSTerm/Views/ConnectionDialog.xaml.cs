using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using WinSTerm.Models;
using WinSTerm.ViewModels;

namespace WinSTerm.Views;

public partial class ConnectionDialog : MetroWindow
{
    private readonly ConnectionDialogViewModel _viewModel;

    public ConnectionDialog(ConnectionInfo? existing = null)
    {
        _viewModel = new ConnectionDialogViewModel(existing);
        DataContext = _viewModel;
        InitializeComponent();

        // Sync initial password into the PasswordBox
        if (!string.IsNullOrEmpty(_viewModel.Password))
        {
            PasswordField.Password = _viewModel.Password;
        }
    }

    public ConnectionInfo? Result => _viewModel.Result;

    private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            _viewModel.Password = pb.Password;
        }
    }

    private void PasswordRadio_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedAuthMethod = AuthMethod.Password;
    }

    private void PrivateKeyRadio_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedAuthMethod = AuthMethod.PrivateKey;
    }
}
