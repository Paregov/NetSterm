using System.Windows;
using MahApps.Metro.Controls;
using WinSTerm.Services;
using WinSTerm.ViewModels;

namespace WinSTerm.Views;

public partial class SettingsDialog : MetroWindow
{
    private bool _suppressToggle;

    public SettingsDialog()
    {
        DataContext = new SettingsDialogViewModel();
        InitializeComponent();
    }

    private void MasterPasswordToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle) return;
        if (!IsLoaded) return;
        if (DataContext is not SettingsDialogViewModel vm) return;
        if (sender is not ToggleSwitch toggle) return;

        if (toggle.IsOn)
        {
            var dialog = new SetMasterPasswordDialog(PasswordDialogMode.Set) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                MasterPasswordService.SetPassword(dialog.NewPassword);
                ShowSecurityStatus("Master password enabled.");
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
                var dialog = new SetMasterPasswordDialog(PasswordDialogMode.Verify) { Owner = this };
                if (dialog.ShowDialog() == true && MasterPasswordService.RemovePassword(dialog.CurrentPassword))
                {
                    ShowSecurityStatus("Master password removed.");
                }
                else
                {
                    if (dialog.DialogResult == true)
                    {
                        MessageBox.Show("Incorrect password.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    SetToggleSuppressed(toggle, true);
                    vm.IsMasterPasswordEnabled = true;
                }
            }
        }
    }

    private void ChangeMasterPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SetMasterPasswordDialog(PasswordDialogMode.Change) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (MasterPasswordService.ChangePassword(dialog.CurrentPassword, dialog.NewPassword))
        {
            ShowSecurityStatus("Master password changed.");
        }
        else
        {
            MessageBox.Show("Current password is incorrect.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetToggleSuppressed(ToggleSwitch toggle, bool isOn)
    {
        _suppressToggle = true;
        toggle.IsOn = isOn;
        _suppressToggle = false;
    }

    private void ShowSecurityStatus(string message)
    {
        SecurityStatusText.Text = "\u2713 " + message;
        SecurityStatusText.Visibility = Visibility.Visible;
    }
}
