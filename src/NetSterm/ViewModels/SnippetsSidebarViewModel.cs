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
        var expandedIds = new HashSet<string>();
        CollectExpandedIds(SnippetTree, expandedIds);

        SnippetTree.Clear();
        var store = _storage.Store;

        var rootItems = new List<SnippetTreeItem>();

        // Build folder map
        var folderMap = new Dictionary<string, SnippetTreeItem>();
        foreach (var folder in store.Folders.OrderBy(f => f.SortOrder))
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
        foreach (var folder in store.Folders.OrderBy(f => f.SortOrder))
        {
            if (folder.ParentFolderId != null && folderMap.TryGetValue(folder.ParentFolderId, out var parentItem))
                parentItem.Children.Add(folderMap[folder.Id]);
            else
                rootItems.Add(folderMap[folder.Id]);
        }

        // Add snippets
        foreach (var snippet in store.Snippets.OrderBy(s => s.SortOrder))
        {
            var item = new SnippetTreeItem
            {
                Id = snippet.Id,
                Name = snippet.Name,
                IsFolder = false,
                Snippet = snippet
            };

            if (snippet.FolderId != null && folderMap.TryGetValue(snippet.FolderId, out var folderItem))
                folderItem.Children.Add(item);
            else
                rootItems.Add(item);
        }

        foreach (var item in rootItems)
            SnippetTree.Add(item);

        RestoreExpandedIds(SnippetTree, expandedIds);
    }

    private static void CollectExpandedIds(IEnumerable<SnippetTreeItem> items, HashSet<string> ids)
    {
        foreach (var item in items)
        {
            if (item.IsFolder && item.IsExpanded)
                ids.Add(item.Id);
            CollectExpandedIds(item.Children, ids);
        }
    }

    private static void RestoreExpandedIds(IEnumerable<SnippetTreeItem> items, HashSet<string> ids)
    {
        if (ids.Count == 0)
            return;
        foreach (var item in items)
        {
            if (item.IsFolder)
                item.IsExpanded = ids.Contains(item.Id);
            RestoreExpandedIds(item.Children, ids);
        }
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
        if (newItem == null)
            return;

        newItem.IsEditing = true;
        newItem.IsSelected = true;

        if (parentFolderId != null)
        {
            var parent = FindTreeItem(parentFolderId);
            if (parent != null)
                parent.IsExpanded = true;
        }
    }

    public bool CommitRename(SnippetTreeItem item)
    {
        var newName = item.Name.Trim();
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        string? folderId;
        string? excludeId;

        if (item.IsFolder)
        {
            var folder = _storage.Store.Folders.FirstOrDefault(f => f.Id == item.Id);
            if (folder == null)
                return false;
            folderId = folder.ParentFolderId;
            excludeId = folder.Id;
        }
        else
        {
            folderId = item.Snippet?.FolderId;
            excludeId = item.Snippet?.Id;
        }

        if (IsDuplicateSnippetName(newName, folderId, excludeId))
        {
            return false;
        }

        if (item.IsFolder)
        {
            var folder = _storage.Store.Folders.First(f => f.Id == item.Id);
            folder.Name = newName;
        }
        else if (item.Snippet != null)
        {
            var snippet = _storage.Store.Snippets.FirstOrDefault(s => s.Id == item.Snippet.Id);
            if (snippet != null)
                snippet.Name = newName;
        }

        _storage.Save();
        return true;
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

    public void MoveSnippetItem(SnippetTreeItem item, string? newParentFolderId)
    {
        if (item.IsFolder)
        {
            var folder = _storage.Store.Folders.FirstOrDefault(f => f.Id == item.Id);
            if (folder == null)
                return;
            if (folder.ParentFolderId == newParentFolderId)
                return;
            folder.ParentFolderId = newParentFolderId;
            ReassignSortOrders(newParentFolderId);
            _storage.Save();
        }
        else if (item.Snippet != null)
        {
            var snippet = _storage.Store.Snippets.FirstOrDefault(s => s.Id == item.Snippet.Id);
            if (snippet == null)
                return;
            if (snippet.FolderId == newParentFolderId)
                return;
            snippet.FolderId = newParentFolderId;
            ReassignSortOrders(newParentFolderId);
            _storage.Save();
        }
        LoadTree();
    }

    private void ReassignSortOrders(string? parentFolderId)
    {
        int order = 0;
        foreach (var folder in _storage.Store.Folders
            .Where(f => f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.SortOrder))
        {
            folder.SortOrder = order++;
        }
        foreach (var snippet in _storage.Store.Snippets
            .Where(s => s.FolderId == parentFolderId)
            .OrderBy(s => s.SortOrder))
        {
            snippet.SortOrder = order++;
        }
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
            if (item.Id == id)
                return item;
            var found = FindInCollection(item.Children, id);
            if (found != null)
                return found;
        }
        return null;
    }
}
