using System.Windows;
using System.Windows.Threading;
using ControlzEx.Theming;

namespace WinSTerm;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Current.ChangeTheme(this, "Dark.Blue");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled UI exception: {e.Exception}");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "WinSTerm Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {ex}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }
}
