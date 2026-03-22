using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;

namespace WinSTerm.Views;

public enum PasswordDialogMode
{
    Set,
    Change,
    Verify
}

public partial class SetMasterPasswordDialog : MetroWindow
{
    private const int MinPasswordLength = 4;
    private readonly PasswordDialogMode _mode;

    public string NewPassword { get; private set; } = "";
    public string CurrentPassword { get; private set; } = "";

    public SetMasterPasswordDialog(PasswordDialogMode mode = PasswordDialogMode.Set)
    {
        InitializeComponent();
        _mode = mode;

        switch (mode)
        {
            case PasswordDialogMode.Set:
                Title = "Set Master Password";
                CurrentPasswordPanel.Visibility = Visibility.Collapsed;
                NewPasswordPanel.Visibility = Visibility.Visible;
                ConfirmPasswordPanel.Visibility = Visibility.Visible;
                Height = 280;
                NewPasswordField.Focus();
                break;

            case PasswordDialogMode.Change:
                Title = "Change Master Password";
                CurrentPasswordPanel.Visibility = Visibility.Visible;
                NewPasswordPanel.Visibility = Visibility.Visible;
                ConfirmPasswordPanel.Visibility = Visibility.Visible;
                Height = 340;
                CurrentPasswordField.Focus();
                break;

            case PasswordDialogMode.Verify:
                Title = "Verify Master Password";
                CurrentPasswordPanel.Visibility = Visibility.Visible;
                NewPasswordPanel.Visibility = Visibility.Collapsed;
                ConfirmPasswordPanel.Visibility = Visibility.Collapsed;
                Height = 200;
                CurrentPasswordField.Focus();
                break;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        TryAccept();
    }

    private void PasswordField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryAccept();
    }

    private void TryAccept()
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (_mode is PasswordDialogMode.Change or PasswordDialogMode.Verify)
        {
            if (string.IsNullOrEmpty(CurrentPasswordField.Password))
            {
                ShowError("Current password is required.");
                return;
            }
        }

        if (_mode is PasswordDialogMode.Set or PasswordDialogMode.Change)
        {
            if (NewPasswordField.Password.Length < MinPasswordLength)
            {
                ShowError($"Password must be at least {MinPasswordLength} characters.");
                return;
            }

            if (NewPasswordField.Password != ConfirmPasswordField.Password)
            {
                ShowError("Passwords do not match.");
                return;
            }
        }

        CurrentPassword = CurrentPasswordField.Password;
        NewPassword = NewPasswordField.Password;
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
