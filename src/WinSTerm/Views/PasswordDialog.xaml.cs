using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;

namespace WinSTerm.Views;

public partial class PasswordDialog : MetroWindow
{
    public string EnteredPassword { get; private set; } = "";

    public PasswordDialog(string username, string host)
    {
        InitializeComponent();
        PromptText.Text = $"Password for {username}@{host}:";
        PasswordField.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        EnteredPassword = PasswordField.Password;
        DialogResult = true;
        Close();
    }

    private void PasswordField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OkButton_Click(sender, e);
        }
    }
}
