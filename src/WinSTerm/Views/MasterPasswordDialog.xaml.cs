using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using WinSTerm.Services;

namespace WinSTerm.Views;

public partial class MasterPasswordDialog : MetroWindow
{
    private const int MaxAttempts = 3;

    private int _failedAttempts;

    public bool IsUnlocked { get; private set; }

    public MasterPasswordDialog()
    {
        InitializeComponent();
        PasswordField.Focus();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        TryUnlock();
    }

    private void PasswordField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryUnlock();
    }

    private void TryUnlock()
    {
        var password = PasswordField.Password;
        if (MasterPasswordService.Verify(password))
        {
            IsUnlocked = true;
            DialogResult = true;
            Close();
            return;
        }

        _failedAttempts++;
        var remaining = MaxAttempts - _failedAttempts;
        ErrorText.Text = $"Incorrect password. ({remaining} attempt{(remaining == 1 ? "" : "s")} remaining)";
        ErrorText.Visibility = Visibility.Visible;
        PasswordField.Clear();
        PasswordField.Focus();

        if (_failedAttempts >= MaxAttempts)
        {
            MessageBox.Show(
                "Too many failed attempts. Application will exit.",
                "Access Denied",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
