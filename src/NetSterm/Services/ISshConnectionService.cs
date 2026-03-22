using NetSterm.Models;

namespace NetSterm.Services;

public interface ISshConnectionService : IDisposable
{
    bool IsConnected { get; }
    ConnectionInfo? ConnectionInfo { get; }
    string? LastAuthResponse { get; }

    event Action<string>? DataReceived;
    event Action? Disconnected;
    event Action<string, bool>? AuthPromptReceived;
    event Action<string>? CurrentDirectoryChanged;

    Task ConnectAsync(ConnectionInfo info);
    Task ConnectAsync(ConnectionInfo info, string? plainPassword);
    void ProvideAuthResponse(string response);
    void UpdateCurrentDirectory(string path);
    void SendData(string data);
    void Resize(uint cols, uint rows);
    void Disconnect();
}
