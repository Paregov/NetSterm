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

    public ConnectionInfo ConnectionInfo { get; }
    public SshConnectionService SshService { get; } = new();
    public SftpService SftpService { get; } = new();
    public SftpBrowserViewModel SftpBrowserViewModel { get; } = new();
    public string TabId { get; } = Guid.NewGuid().ToString();

    public SessionTabViewModel(ConnectionInfo info)
    {
        ConnectionInfo = info;
        Title = info.Name;
        SshService.Disconnected += OnDisconnected;
    }

    private void OnDisconnected()
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsConnected = false;
            StatusText = "Connection lost";
        });
    }

    public async Task ConnectAsync(string? password = null)
    {
        try
        {
            IsConnecting = true;
            StatusText = "Connecting...";

            await SshService.ConnectAsync(ConnectionInfo, password);
            IsConnected = true;
            StatusText = $"Connected to {ConnectionInfo.Host}";

            // Also connect SFTP
            try
            {
                await SftpService.ConnectAsync(ConnectionInfo, password);
                SftpBrowserViewModel.AttachService(SftpService);
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
        SshService.Disconnect();
        SftpService.Disconnect();
        IsConnected = false;
        StatusText = "Disconnected";
    }

    public void Dispose()
    {
        SshService.Disconnected -= OnDisconnected;
        SshService.Dispose();
        SftpService.Dispose();
    }
}
