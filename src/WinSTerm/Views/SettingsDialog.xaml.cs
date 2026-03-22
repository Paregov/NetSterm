using MahApps.Metro.Controls;
using WinSTerm.ViewModels;

namespace WinSTerm.Views;

public partial class SettingsDialog : MetroWindow
{
    public SettingsDialog()
    {
        DataContext = new SettingsDialogViewModel();
        InitializeComponent();
    }
}
