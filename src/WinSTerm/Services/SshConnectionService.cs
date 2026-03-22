using Renci.SshNet;
using WinSTerm.Models;

namespace WinSTerm.Services;

public class SshConnectionService : IDisposable
{
    private SshClient? _sshClient;
    private ShellStream? _shellStream;
    private CancellationTokenSource? _readCts;
    private Models.ConnectionInfo? _connectionInfo;

    public bool IsConnected => _sshClient?.IsConnected == true;
    public Models.ConnectionInfo? ConnectionInfo => _connectionInfo;

    public event Action<string>? DataReceived;
    public event Action? Disconnected;

    public Task ConnectAsync(Models.ConnectionInfo info)
    {
        return ConnectAsync(info, null);
    }

    public Task ConnectAsync(Models.ConnectionInfo info, string? plainPassword)
    {
        return Task.Run(() =>
        {
            _connectionInfo = info;

            var authMethods = new List<AuthenticationMethod>();
            if (info.AuthMethod == AuthMethod.PrivateKey && !string.IsNullOrEmpty(info.PrivateKeyPath))
            {
                var keyFile = new PrivateKeyFile(info.PrivateKeyPath);
                authMethods.Add(new PrivateKeyAuthenticationMethod(info.Username, keyFile));
            }
            else
            {
                var password = plainPassword
                    ?? (!string.IsNullOrEmpty(info.EncryptedPassword)
                        ? ConnectionStorageService.DecryptPassword(info.EncryptedPassword)
                        : "");
                authMethods.Add(new PasswordAuthenticationMethod(info.Username, password));
            }

            var connInfo = new Renci.SshNet.ConnectionInfo(info.Host, info.Port, info.Username, authMethods.ToArray());
            _sshClient = new SshClient(connInfo);
            _sshClient.Connect();

            _shellStream = _sshClient.CreateShellStream("xterm-256color", 80, 24, 800, 600, 4096);

            _readCts = new CancellationTokenSource();
            StartReadLoop(_readCts.Token);
        });
    }

    public void SendData(string data)
    {
        if (_shellStream is { CanWrite: true })
        {
            _shellStream.Write(data);
            _shellStream.Flush();
        }
    }

    public void Resize(uint cols, uint rows)
    {
        _shellStream?.ChangeWindowSize(cols, rows, cols * 10, rows * 10);
    }

    public void Disconnect()
    {
        _readCts?.Cancel();

        if (_shellStream != null)
        {
            _shellStream.Close();
            _shellStream.Dispose();
            _shellStream = null;
        }

        if (_sshClient != null)
        {
            if (_sshClient.IsConnected)
                _sshClient.Disconnect();
            _sshClient.Dispose();
            _sshClient = null;
        }

        _connectionInfo = null;
    }

    private void StartReadLoop(CancellationToken ct)
    {
        Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested && _shellStream != null)
                {
                    var count = await _shellStream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (count > 0)
                    {
                        var text = System.Text.Encoding.UTF8.GetString(buffer, 0, count);
                        DataReceived?.Invoke(text);
                    }
                    else
                    {
                        await Task.Delay(10, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                Disconnected?.Invoke();
            }
        }, ct);
    }

    public void Dispose()
    {
        Disconnect();
        _readCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
