using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NetSterm.Views;

public partial class PasswordDialog : Window
{
    public string EnteredPassword { get; private set; } = "";

    public PasswordDialog() : this("user", "host")
    {
    }

    public PasswordDialog(string username, string host)
    {
        InitializeComponent();
        PromptText.Text = $"Password for {username}@{host}:";
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PasswordField.Focus();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        EnteredPassword = PasswordField.Text ?? "";
        Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void PasswordField_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OkButton_Click(sender, e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close(false);
        base.OnKeyDown(e);
    }
}
