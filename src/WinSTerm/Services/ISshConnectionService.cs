using WinSTerm.Models;

namespace WinSTerm.Services;

public interface ISshConnectionService : IDisposable
{
    bool IsConnected { get; }
    ConnectionInfo? ConnectionInfo { get; }

    event Action<string>? DataReceived;
    event Action? Disconnected;

    Task ConnectAsync(ConnectionInfo info);
    Task ConnectAsync(ConnectionInfo info, string? plainPassword);
    void SendData(string data);
    void Resize(uint cols, uint rows);
    void Disconnect();
}
