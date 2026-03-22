namespace WinSTerm.Models;

public enum AuthMethod { Password, PrivateKey }

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
}
