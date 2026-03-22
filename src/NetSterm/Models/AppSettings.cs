using System.Text.Json;

namespace NetSterm.Models;

public class AppSettings
{
    // Terminal
    public string FontFamily { get; set; } = "Cascadia Code";
    public int FontSize { get; set; } = 14;
    public int ScrollbackLines { get; set; } = 10000;
    public string CursorStyle { get; set; } = "block";
    public bool CursorBlink { get; set; } = true;

    // SSH Defaults
    public int DefaultKeepAliveSeconds { get; set; } = 30;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public bool DefaultCompression { get; set; }

    // Appearance
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowQuickConnect { get; set; } = true;
    public bool ConfirmOnCloseTab { get; set; }
    public bool ConfirmOnExit { get; set; } = true;

    // SFTP
    public string DefaultLocalDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public bool ShowHiddenFiles { get; set; }

    // Security / Master Password
    public bool IsMasterPasswordEnabled { get; set; }
    public string? MasterPasswordHash { get; set; }
    public string? MasterPasswordSalt { get; set; }

    public AppSettings Clone()
    {
        return JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
    }
}
