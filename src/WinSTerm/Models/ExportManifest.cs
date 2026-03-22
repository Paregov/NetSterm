namespace WinSTerm.Models;

public class ExportManifest
{
    public string Version { get; set; } = "2.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public bool ContainsPasswords { get; set; }
    public bool ContainsPrivateKeys { get; set; }
    public int ConnectionCount { get; set; }
    public int SnippetCount { get; set; }
    public int ConnectionFolderCount { get; set; }
    public int SnippetFolderCount { get; set; }
}
