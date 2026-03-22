namespace NetSterm.Models;

public enum AuthMethod { Password, PrivateKey }

public enum ProxyType { None, Socks4, Socks5, Http }

public class ConnectionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;
    public string? PrivateKeyPath { get; set; }
    public string? FolderId { get; set; }
    public string? EncryptedPassword { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastConnectedAt { get; set; }

    // Terminal settings
    public string? TerminalType { get; set; } = "xterm-256color";
    public string? StartupCommand { get; set; }
    public string? RemoteDirectory { get; set; }

    // Network settings
    public int KeepAliveInterval { get; set; } = 30;
    public bool EnableCompression { get; set; }

    // Jump host (SSH gateway/bastion)
    public string? JumpHost { get; set; }
    public int JumpPort { get; set; } = 22;
    public string? JumpUsername { get; set; }

    // Proxy settings
    public ProxyType ProxyType { get; set; } = ProxyType.None;
    public string? ProxyHost { get; set; }
    public int ProxyPort { get; set; } = 1080;

    // Metadata
    public string? Description { get; set; }
}
