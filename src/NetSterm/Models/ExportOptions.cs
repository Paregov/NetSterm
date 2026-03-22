namespace NetSterm.Models;

public class ExportOptions
{
    public bool IncludeConnections { get; set; } = true;
    public bool IncludeSnippets { get; set; } = true;
    public bool IncludePasswords { get; set; }
    public bool IncludePrivateKeys { get; set; } = true;

    /// <summary>Granular selection: if empty, export ALL items of that type.</summary>
    public HashSet<string> SelectedConnectionIds { get; set; } = [];
    public HashSet<string> SelectedConnectionFolderIds { get; set; } = [];
    public HashSet<string> SelectedSnippetIds { get; set; } = [];
    public HashSet<string> SelectedSnippetFolderIds { get; set; } = [];
}
