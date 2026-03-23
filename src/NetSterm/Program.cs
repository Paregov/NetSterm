using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.WebView.Desktop;
using Serilog;

namespace NetSterm;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetSterm", "logs", "NetSterm-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        Log.Information("NetSterm starting up");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.Information("NetSterm shutting down");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseDesktopWebView();
}
