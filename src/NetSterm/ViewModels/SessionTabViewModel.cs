using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using NetSterm.Models;
using NetSterm.Services;

namespace NetSterm.ViewModels;

public partial class SessionTabViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private string _title = "New Session";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _statusText = "Disconnected";
    [ObservableProperty] private string _currentRemoteDirectory = "/";

    public ConnectionInfo ConnectionInfo { get; }
    public SshConnectionService SshService { get; } = new();
    public SftpService SftpService { get; } = new();
    public SftpBrowserViewModel SftpBrowserViewModel { get; } = new();
    public string TabId { get; } = Guid.NewGuid().ToString();
    public TaskCompletionSource<bool> TerminalReady { get; } = new();

    public SessionTabViewModel(ConnectionInfo info)
    {
        ConnectionInfo = info;
        Title = info.Name;
        SshService.Disconnected += OnDisconnected;
        SshService.CurrentDirectoryChanged += OnCurrentDirectoryChanged;
    }

    private void OnDisconnected()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = false;
            StatusText = "Connection lost";
        });
    }

    private void OnCurrentDirectoryChanged(string path)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentRemoteDirectory = path;
        });
    }

    public async Task ConnectAsync(string? password = null)
    {
        try
        {
            IsConnecting = true;
            StatusText = "Connecting...";
            Log.Information("Session connecting to {Host}:{Port}", ConnectionInfo.Host, ConnectionInfo.Port);

            // Wait for terminal WebView to initialize before connecting.
            // Keyboard-interactive auth prompts write to the terminal during connect.
            try
            {
                await TerminalReady.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Log.Debug("Terminal WebView ready for {Host}", ConnectionInfo.Host);
            }
            catch (TimeoutException)
            {
                Log.Warning("Terminal WebView timed out for {Host}, proceeding anyway", ConnectionInfo.Host);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Terminal WebView initialization failed for {Host}", ConnectionInfo.Host);
            }

            await SshService.ConnectAsync(ConnectionInfo, password);
            IsConnected = true;
            StatusText = $"Connected to {ConnectionInfo.Host}";
            Log.Information("SSH connected to {Host}:{Port}", ConnectionInfo.Host, ConnectionInfo.Port);

            // Connect SFTP using the provided password or the one captured
            // from keyboard-interactive authentication.
            try
            {
                var sftpPassword = password ?? SshService.LastAuthResponse;
                await SftpService.ConnectAsync(ConnectionInfo, sftpPassword);
                SftpBrowserViewModel.AttachService(SftpService);
                // Re-notify so the SFTP sidebar picks up that SFTP is now connected
                OnPropertyChanged(nameof(IsConnected));
                Log.Information("SFTP connected to {Host}", ConnectionInfo.Host);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SFTP connection failed for {Host} (non-fatal)", ConnectionInfo.Host);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Session connection failed for {Host}:{Port}", ConnectionInfo.Host, ConnectionInfo.Port);
            StatusText = "Connection failed";
            IsConnected = false;
            Disconnect();
            throw;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    public void Disconnect()
    {
        SshService.Disconnected -= OnDisconnected;
        SshService.CurrentDirectoryChanged -= OnCurrentDirectoryChanged;
        SshService.Disconnect();
        SftpService.Disconnect();
        IsConnected = false;
        StatusText = "Disconnected";
    }

    public void Dispose()
    {
        SshService.Disconnected -= OnDisconnected;
        SshService.CurrentDirectoryChanged -= OnCurrentDirectoryChanged;
        SshService.Dispose();
        SftpService.Dispose();
    }
}
