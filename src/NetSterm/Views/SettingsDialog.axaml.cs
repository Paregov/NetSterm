using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NetSterm.Services;
using NetSterm.ViewModels;

namespace NetSterm.Views;

public partial class SettingsDialog : Window
{
    private bool _suppressToggle;
    private bool _initialized;

    public SettingsDialog()
    {
        DataContext = new SettingsDialogViewModel();
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _initialized = true;
        MasterPasswordToggle.PropertyChanged += MasterPasswordToggle_PropertyChanged;
    }

    private async void MasterPasswordToggle_PropertyChanged(
        object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ToggleSwitch.IsCheckedProperty) return;
        if (_suppressToggle || !_initialized) return;
        if (DataContext is not SettingsDialogViewModel vm) return;
        if (sender is not ToggleSwitch toggle) return;

        if (toggle.IsChecked == true)
        {
            var dialog = new SetMasterPasswordDialog(PasswordDialogMode.Set);
            var result = await dialog.ShowDialog<bool>(this);
            if (result)
            {
                MasterPasswordService.SetPassword(dialog.NewPassword);
                ShowSecurityStatus("\u2713 Master password enabled.", isError: false);
            }
            else
            {
                SetToggleSuppressed(toggle, false);
                vm.IsMasterPasswordEnabled = false;
            }
        }
        else
        {
            if (MasterPasswordService.IsEnabled)
            {
                var dialog = new SetMasterPasswordDialog(PasswordDialogMode.Verify);
                var result = await dialog.ShowDialog<bool>(this);
                if (result && MasterPasswordService.RemovePassword(dialog.CurrentPassword))
                {
                    ShowSecurityStatus("\u2713 Master password removed.", isError: false);
                }
                else
                {
                    if (result)
                    {
                        ShowSecurityStatus("\u2717 Incorrect password.", isError: true);
                    }
                    SetToggleSuppressed(toggle, true);
                    vm.IsMasterPasswordEnabled = true;
                }
            }
        }
    }

    private async void ChangeMasterPassword_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SetMasterPasswordDialog(PasswordDialogMode.Change);
        var result = await dialog.ShowDialog<bool>(this);
        if (!result) return;

        if (MasterPasswordService.ChangePassword(dialog.CurrentPassword, dialog.NewPassword))
        {
            ShowSecurityStatus("\u2713 Master password changed.", isError: false);
        }
        else
        {
            ShowSecurityStatus("\u2717 Current password is incorrect.", isError: true);
        }
    }

    private void SetToggleSuppressed(ToggleSwitch toggle, bool isOn)
    {
        _suppressToggle = true;
        toggle.IsChecked = isOn;
        _suppressToggle = false;
    }

    private void ShowSecurityStatus(string message, bool isError)
    {
        SecurityStatusText.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(isError ? "#E74C3C" : "#27AE60"));
        SecurityStatusText.Text = message;
        SecurityStatusText.IsVisible = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
