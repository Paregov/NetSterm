namespace WinSTerm.Models;

public class ConnectionStore
{
    public List<ConnectionFolder> Folders { get; set; } = new();
    public List<ConnectionInfo> Connections { get; set; } = new();
}
