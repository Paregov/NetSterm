using Renci.SshNet;
using Renci.SshNet.Common;
using WinSTerm.Models;

namespace WinSTerm.Services;

public class SshConnectionService : ISshConnectionService
{
    private SshClient? _sshClient;
    private ShellStream? _shellStream;
    private CancellationTokenSource? _readCts;
    private Models.ConnectionInfo? _connectionInfo;
    private volatile ManualResetEventSlim? _authResponseWait;
    private string? _authResponse;

    public bool IsConnected => _sshClient?.IsConnected == true;
    public Models.ConnectionInfo? ConnectionInfo => _connectionInfo;
    public string? LastAuthResponse { get; private set; }

    public event Action<string>? DataReceived;
    public event Action? Disconnected;
    public event Action<string, bool>? AuthPromptReceived;

    public Task ConnectAsync(Models.ConnectionInfo info)
    {
        return ConnectAsync(info, null);
    }

    public Task ConnectAsync(Models.ConnectionInfo info, string? plainPassword)
    {
        return Task.Run(() =>
        {
            _connectionInfo = info;
            LastAuthResponse = null;

            var connInfo = ConnectionFactory.Create(info, plainPassword, ConfigureKeyboardInteractive);
            _sshClient = new SshClient(connInfo);

            try
            {
                _sshClient.Connect();
            }
            catch (Renci.SshNet.Common.SshAuthenticationException)
                when (plainPassword == null && LastAuthResponse == null)
            {
                // Auth failed and keyboard-interactive didn't trigger — the server
                // likely only supports password auth.  Prompt in the terminal and retry.
                _sshClient.Dispose();
                _sshClient = null;

                var capturedPassword = PromptPasswordInTerminal();
                LastAuthResponse = capturedPassword;

                var retryConn = ConnectionFactory.Create(info, capturedPassword);
                _sshClient = new SshClient(retryConn);
                _sshClient.Connect();
            }

            _shellStream = _sshClient.CreateShellStream("xterm-256color", 80, 24, 800, 600, 4096);

            _readCts = new CancellationTokenSource();
            StartReadLoop(_readCts.Token);
        });
    }

    private string PromptPasswordInTerminal()
    {
        using var waitHandle = new ManualResetEventSlim(false);
        _authResponseWait = waitHandle;
        _authResponse = null;

        AuthPromptReceived?.Invoke("Password: ", true);

        try
        {
            if (!waitHandle.Wait(TimeSpan.FromSeconds(60)))
                throw new TimeoutException("Password prompt timed out waiting for user input.");
        }
        finally
        {
            _authResponseWait = null;
        }

        return _authResponse ?? "";
    }

    private void ConfigureKeyboardInteractive(KeyboardInteractiveAuthenticationMethod kbdInteractive)
    {
        kbdInteractive.AuthenticationPrompt += OnKeyboardInteractivePrompt;
    }

    private void OnKeyboardInteractivePrompt(object? sender, AuthenticationPromptEventArgs e)
    {
        for (int i = 0; i < e.Prompts.Count; i++)
        {
            var prompt = e.Prompts[i];
            var waitHandle = new ManualResetEventSlim(false);
            _authResponseWait = waitHandle;
            _authResponse = null;

            var displayText = prompt.Request;
            if (i == 0 && !string.IsNullOrEmpty(e.Instruction))
            {
                displayText = e.Instruction + "\r\n" + displayText;
            }

            AuthPromptReceived?.Invoke(displayText, !prompt.IsEchoed);

            try
            {
                if (!waitHandle.Wait(TimeSpan.FromSeconds(60)))
                {
                    throw new TimeoutException("Authentication prompt timed out waiting for user input.");
                }
            }
            finally
            {
                _authResponseWait = null;
                waitHandle.Dispose();
            }

            prompt.Response = _authResponse ?? "";
        }
    }

    public void ProvideAuthResponse(string response)
    {
        _authResponse = response;
        LastAuthResponse = response;
        _authResponseWait?.Set();
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

        // Unblock any waiting auth prompt so the SSH thread does not hang
        try { _authResponseWait?.Set(); }
        catch (ObjectDisposedException) { }

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
