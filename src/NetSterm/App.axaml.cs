using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaWebView;
using Serilog;
using NetSterm.Services;

namespace NetSterm;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
        AvaloniaWebViewBuilder.Initialize(default);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            if (MasterPasswordService.IsEnabled)
            {
                // TODO: Avalonia migration - Show master password dialog (will be ported by dialog agent)
                // For now, proceed directly
                MasterPasswordService.UnlockWithoutPassword();
            }
            else
            {
                MasterPasswordService.UnlockWithoutPassword();
            }

            desktop.MainWindow = new MainWindow();
        }

        Log.Information("Application initialized");
        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "Unhandled domain exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
