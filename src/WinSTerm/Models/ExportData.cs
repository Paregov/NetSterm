namespace WinSTerm.Models;

public class ExportData
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<ConnectionFolder> Folders { get; set; } = new();
    public List<ConnectionInfo> Connections { get; set; } = new();
}
