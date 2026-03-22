using System.Windows;
using ControlzEx.Theming;

namespace WinSTerm;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Current.ChangeTheme(this, "Dark.Blue");
    }
}
