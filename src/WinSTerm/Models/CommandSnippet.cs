namespace WinSTerm.Models;

public class CommandSnippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? FolderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
