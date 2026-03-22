using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WinSTerm.Services;

namespace WinSTerm.Views;

public partial class MasterPasswordDialog : Window
{
    private const int MaxAttempts = 3;
    private int _failedAttempts;

    public bool IsUnlocked { get; private set; }

    public MasterPasswordDialog()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PasswordField.Focus();
    }

    private void Unlock_Click(object? sender, RoutedEventArgs e)
    {
        TryUnlock();
    }

    private void PasswordField_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryUnlock();
    }

    private void TryUnlock()
    {
        var password = PasswordField.Text ?? "";
        if (MasterPasswordService.Verify(password))
        {
            IsUnlocked = true;
            Close(true);
            return;
        }

        _failedAttempts++;
        var remaining = MaxAttempts - _failedAttempts;
        ErrorText.Text = $"Incorrect password. ({remaining} attempt{(remaining == 1 ? "" : "s")} remaining)";
        ErrorText.IsVisible = true;
        PasswordField.Text = "";
        PasswordField.Focus();

        if (_failedAttempts >= MaxAttempts)
        {
            ErrorText.Text = "Too many failed attempts. Application will exit.";
            Close(false);
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
