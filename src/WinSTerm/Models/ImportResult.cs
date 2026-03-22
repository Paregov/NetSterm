namespace WinSTerm.Models;

public class ImportResult
{
    public int ConnectionsAdded { get; set; }
    public int ConnectionFoldersAdded { get; set; }
    public int SnippetsAdded { get; set; }
    public int SnippetFoldersAdded { get; set; }
    public bool ContainedPasswords { get; set; }
    public bool ContainedPrivateKeys { get; set; }
    public int PrivateKeysImported { get; set; }
}
