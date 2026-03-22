using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetSterm.Models;
using NetSterm.Services;

namespace NetSterm.ViewModels;

public partial class SnippetsSidebarViewModel : ObservableObject
{
    private readonly SnippetStorageService _storage = SnippetStorageService.Instance;

    public ObservableCollection<SnippetTreeItem> SnippetTree { get; } = [];

    public event Action<string>? SnippetExecuteRequested;

    public SnippetsSidebarViewModel()
    {
        LoadTree();
    }

    public void LoadTree()
    {
        SnippetTree.Clear();
        var store = _storage.Store;

        var rootItems = new List<SnippetTreeItem>();

        // Build folder map
        var folderMap = new Dictionary<string, SnippetTreeItem>();
        foreach (var folder in store.Folders)
        {
            var item = new SnippetTreeItem
            {
                Id = folder.Id,
                Name = folder.Name,
                IsFolder = true,
                IsExpanded = folder.IsExpanded
            };
            folderMap[folder.Id] = item;
        }

        // Nest folders
        foreach (var folder in store.Folders)
        {
            if (folder.ParentFolderId != null && folderMap.ContainsKey(folder.ParentFolderId))
                folderMap[folder.ParentFolderId].Children.Add(folderMap[folder.Id]);
            else
                rootItems.Add(folderMap[folder.Id]);
        }

        // Add snippets
        foreach (var snippet in store.Snippets)
        {
            var item = new SnippetTreeItem
            {
                Id = snippet.Id,
                Name = snippet.Name,
                IsFolder = false,
                Snippet = snippet
            };

            if (snippet.FolderId != null && folderMap.ContainsKey(snippet.FolderId))
                folderMap[snippet.FolderId].Children.Add(item);
            else
                rootItems.Add(item);
        }

        foreach (var item in rootItems)
            SnippetTree.Add(item);
    }

    public void AddSnippet(CommandSnippet snippet, string? folderId = null)
    {
        snippet.FolderId = folderId;
        _storage.AddSnippet(snippet);
        LoadTree();
    }

    public void UpdateSnippet(CommandSnippet snippet)
    {
        _storage.UpdateSnippet(snippet);
        LoadTree();
    }

    public void DeleteSnippet(string snippetId)
    {
        _storage.DeleteSnippet(snippetId);
        LoadTree();
    }

    public void AddFolderWithInPlaceEdit(string? parentFolderId)
    {
        var folder = new SnippetFolder { Name = "New Folder", ParentFolderId = parentFolderId };
        _storage.AddFolder(folder);
        LoadTree();

        var newItem = FindTreeItem(folder.Id);
        if (newItem == null) return;

        newItem.IsEditing = true;
        newItem.IsSelected = true;

        if (parentFolderId != null)
        {
            var parent = FindTreeItem(parentFolderId);
            if (parent != null) parent.IsExpanded = true;
        }
    }

    public void CommitFolderRename(SnippetTreeItem item)
    {
        if (!item.IsFolder || string.IsNullOrWhiteSpace(item.Name)) return;

        var folder = _storage.Store.Folders.FirstOrDefault(f => f.Id == item.Id);
        if (folder == null) return;

        folder.Name = item.Name.Trim();
        _storage.Save();
    }

    public void CancelFolderRename()
    {
        LoadTree();
    }

    public void DeleteItem(SnippetTreeItem item)
    {
        if (item.IsFolder)
            _storage.DeleteFolder(item.Id);
        else
            _storage.DeleteSnippet(item.Id);
        LoadTree();
    }

    public void ExecuteSnippet(CommandSnippet snippet)
    {
        SnippetExecuteRequested?.Invoke(snippet.Command);
    }

    private SnippetTreeItem? FindTreeItem(string id)
    {
        return FindInCollection(SnippetTree, id);
    }

    private static SnippetTreeItem? FindInCollection(IEnumerable<SnippetTreeItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id) return item;
            var found = FindInCollection(item.Children, id);
            if (found != null) return found;
        }
        return null;
    }
}
