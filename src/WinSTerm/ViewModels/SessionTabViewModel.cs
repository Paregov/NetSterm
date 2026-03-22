using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

public partial class SessionTabViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private string _title = "New Session";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isConnecting;
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

            // Wait for terminal WebView2 to initialize before connecting.
            // Keyboard-interactive auth prompts write to the terminal during connect.
            await TerminalReady.Task.WaitAsync(TimeSpan.FromSeconds(10));

            await SshService.ConnectAsync(ConnectionInfo, password);
            IsConnected = true;
            StatusText = $"Connected to {ConnectionInfo.Host}";

            // Connect SFTP using the provided password or the one captured
            // from keyboard-interactive authentication.
            try
            {
                var sftpPassword = password ?? SshService.LastAuthResponse;
                await SftpService.ConnectAsync(ConnectionInfo, sftpPassword);
                SftpBrowserViewModel.AttachService(SftpService);
                // Re-notify so the SFTP sidebar picks up that SFTP is now connected
                OnPropertyChanged(nameof(IsConnected));
            }
            catch { /* SFTP is optional - don't fail the session */ }
        }
        catch (Exception)
        {
            StatusText = "Connection failed";
            IsConnected = false;
            Disconnect();
            throw; // Re-throw so callers can show error popup
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
