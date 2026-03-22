using WinSTerm.Models;

namespace WinSTerm.Services;

public interface ISshConnectionService : IDisposable
{
    bool IsConnected { get; }
    ConnectionInfo? ConnectionInfo { get; }
    string? LastAuthResponse { get; }

    event Action<string>? DataReceived;
    event Action? Disconnected;
    event Action<string, bool>? AuthPromptReceived;

    Task ConnectAsync(ConnectionInfo info);
    Task ConnectAsync(ConnectionInfo info, string? plainPassword);
    void ProvideAuthResponse(string response);
    void SendData(string data);
    void Resize(uint cols, uint rows);
    void Disconnect();
}
