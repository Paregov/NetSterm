using NetSterm.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using Serilog;

namespace NetSterm.Services;

public class SshConnectionService : ISshConnectionService
{
    private SshClient? _sshClient;
    private ShellStream? _shellStream;
    private CancellationTokenSource? _readCts;
    private Models.ConnectionInfo? _connectionInfo;
    private volatile ManualResetEventSlim? _authResponseWait;
    private string? _authResponse;
    private string? _currentDirectory;

    public bool IsConnected => _sshClient?.IsConnected == true;
    public Models.ConnectionInfo? ConnectionInfo => _connectionInfo;
    public string? LastAuthResponse { get; private set; }

    public event Action<string>? DataReceived;
    public event Action? Disconnected;
    public event Action<string, bool>? AuthPromptReceived;
    public event Action<string>? CurrentDirectoryChanged;

    public Task ConnectAsync(Models.ConnectionInfo info)
    {
        return ConnectAsync(info, null);
    }

    public Task ConnectAsync(Models.ConnectionInfo info, string? plainPassword)
    {
        return Task.Run(() =>
        {
            Log.Information("SSH connecting to {Host}:{Port} as {User}", info.Host, info.Port, info.Username);
            _connectionInfo = info;
            LastAuthResponse = null;

            Log.Information("SSH connecting to {Host}:{Port} as {User}", info.Host, info.Port, info.Username);

            var connInfo = ConnectionFactory.Create(info, plainPassword, ConfigureKeyboardInteractive);
            _sshClient = new SshClient(connInfo);

            try
            {
                _sshClient.Connect();
                Log.Debug("SSH client connected to {Host}:{Port}", info.Host, info.Port);
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
                when (plainPassword == null && LastAuthResponse == null)
            {
                // Auth failed and keyboard-interactive didn't trigger — the server
                // likely only supports password auth.  Prompt in the terminal and retry.
                Log.Warning(ex, "Initial auth failed for {Host}, prompting for password", info.Host);
                _sshClient.Dispose();
                _sshClient = null;

                var capturedPassword = PromptPasswordInTerminal();
                LastAuthResponse = capturedPassword;

                var retryConn = ConnectionFactory.Create(info, capturedPassword);
                _sshClient = new SshClient(retryConn);
                _sshClient.Connect();
                Log.Debug("SSH client connected on retry to {Host}:{Port}", info.Host, info.Port);
            }

            _shellStream = _sshClient.CreateShellStream("xterm-256color", 80, 24, 800, 600, 4096);
            Log.Debug("Shell stream created for {Host}", info.Host);

            // Configure shell to emit OSC 7 with CWD after each command for SFTP sidebar sync
            _shellStream.Write("export PROMPT_COMMAND='printf \"\\033]7;%s\\007\" \"$PWD\"'\n");
            Log.Debug("PROMPT_COMMAND injected for {Host}", info.Host);

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

    public void UpdateCurrentDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path) && path != _currentDirectory)
        {
            _currentDirectory = path;
            CurrentDirectoryChanged?.Invoke(path);
        }
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
        Log.Debug("SSH disconnecting from {Host}", _connectionInfo?.Host);
        _readCts?.Cancel();

        // Unblock any waiting auth prompt so the SSH thread does not hang
        try
        { _authResponseWait?.Set(); }
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
        _currentDirectory = null;
        Log.Debug("SSH disconnect complete");
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
                    var count = await _shellStream.ReadAsync(buffer.AsMemory(), ct);
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
            catch (Exception ex)
            {
                Log.Error(ex, "SSH read loop error for {Host}", _connectionInfo?.Host);
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
