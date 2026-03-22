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
        snippet.Name = GetUniqueSnippetName(snippet.Name, folderId);
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
        var name = GetUniqueSnippetName("New Folder", parentFolderId);
        var folder = new SnippetFolder { Name = name, ParentFolderId = parentFolderId };
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

        var newName = item.Name.Trim();
        if (IsDuplicateSnippetName(newName, folder.ParentFolderId, folder.Id))
        {
            item.Name = folder.Name;
            return;
        }

        folder.Name = newName;
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

    private bool IsDuplicateSnippetName(string name, string? folderId, string? excludeId = null)
    {
        var trimmedName = name.Trim();

        var folders = _storage.Store.Folders
            .Where(f => f.ParentFolderId == folderId && f.Id != excludeId);
        if (folders.Any(f => string.Equals(f.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            return true;

        var snippets = _storage.Store.Snippets
            .Where(s => s.FolderId == folderId && s.Id != excludeId);
        if (snippets.Any(s => string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private string GetUniqueSnippetName(string baseName, string? folderId, string? excludeId = null)
    {
        var name = baseName.Trim();
        if (!IsDuplicateSnippetName(name, folderId, excludeId))
            return name;

        for (int i = 2; i < 1000; i++)
        {
            var candidate = $"{name} ({i})";
            if (!IsDuplicateSnippetName(candidate, folderId, excludeId))
                return candidate;
        }

        return name;
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
