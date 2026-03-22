namespace WinSTerm.Models;

public class SnippetStore
{
    public List<SnippetFolder> Folders { get; set; } = [];
    public List<CommandSnippet> Snippets { get; set; } = [];
}
