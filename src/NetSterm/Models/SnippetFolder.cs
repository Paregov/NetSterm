namespace NetSterm.Models;

public class SnippetFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string? ParentFolderId { get; set; }
    public bool IsExpanded { get; set; } = true;
    public int SortOrder { get; set; }
}
