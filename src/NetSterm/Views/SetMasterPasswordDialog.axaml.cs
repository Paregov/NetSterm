using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NetSterm.Views;

public enum PasswordDialogMode
{
    Set,
    Change,
    Verify
}

public partial class SetMasterPasswordDialog : Window
{
    private const int MinPasswordLength = 4;
    private readonly PasswordDialogMode _mode;

    public string NewPassword { get; private set; } = "";
    public string CurrentPassword { get; private set; } = "";

    public SetMasterPasswordDialog() : this(PasswordDialogMode.Set)
    {
    }

    public SetMasterPasswordDialog(PasswordDialogMode mode)
    {
        InitializeComponent();
        _mode = mode;

        switch (mode)
        {
            case PasswordDialogMode.Set:
                Title = "Set Master Password";
                CurrentPasswordPanel.IsVisible = false;
                NewPasswordPanel.IsVisible = true;
                ConfirmPasswordPanel.IsVisible = true;
                Height = 280;
                break;

            case PasswordDialogMode.Change:
                Title = "Change Master Password";
                CurrentPasswordPanel.IsVisible = true;
                NewPasswordPanel.IsVisible = true;
                ConfirmPasswordPanel.IsVisible = true;
                Height = 340;
                break;

            case PasswordDialogMode.Verify:
                Title = "Verify Master Password";
                CurrentPasswordPanel.IsVisible = true;
                NewPasswordPanel.IsVisible = false;
                ConfirmPasswordPanel.IsVisible = false;
                Height = 200;
                break;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_mode == PasswordDialogMode.Set)
            NewPasswordField.Focus();
        else
            CurrentPasswordField.Focus();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        TryAccept();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void PasswordField_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryAccept();
    }

    private void TryAccept()
    {
        ErrorText.IsVisible = false;

        if (_mode is PasswordDialogMode.Change or PasswordDialogMode.Verify)
        {
            if (string.IsNullOrEmpty(CurrentPasswordField.Text))
            {
                ShowError("Current password is required.");
                return;
            }
        }

        if (_mode is PasswordDialogMode.Set or PasswordDialogMode.Change)
        {
            if ((NewPasswordField.Text?.Length ?? 0) < MinPasswordLength)
            {
                ShowError($"Password must be at least {MinPasswordLength} characters.");
                return;
            }

            if (NewPasswordField.Text != ConfirmPasswordField.Text)
            {
                ShowError("Passwords do not match.");
                return;
            }
        }

        CurrentPassword = CurrentPasswordField.Text ?? "";
        NewPassword = NewPasswordField.Text ?? "";
        Close(true);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close(false);
        base.OnKeyDown(e);
    }
}
