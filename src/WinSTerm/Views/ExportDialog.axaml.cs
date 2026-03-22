using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Material.Icons;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.Views;

public partial class ExportDialog : Window
{
    public ExportOptions? Result { get; private set; }
    public ObservableCollection<ExportTreeItem> TreeItems { get; } = [];

    private bool _includePasswords;
    public bool IncludePasswords
    {
        get => _includePasswords;
        set { _includePasswords = value; }
    }

    private bool _includePrivateKeys = true;
    public bool IncludePrivateKeys
    {
        get => _includePrivateKeys;
        set { _includePrivateKeys = value; }
    }

    public ExportDialog()
    {
        InitializeComponent();
        DataContext = this;
        BuildTree();
    }

    private void BuildTree()
    {
        BuildConnectionsTree();
        BuildSnippetsTree();
    }

    private void BuildConnectionsTree()
    {
        var connStorage = new ConnectionStorageService();
        var store = connStorage.Store;

        var connectionsRoot = new ExportTreeItem
        {
            Name = "Connections",
            IsFolder = true,
            ItemType = "connections-root"
        };

        var folderMap = new Dictionary<string, ExportTreeItem>();
        foreach (var folder in store.Folders)
        {
            var item = new ExportTreeItem
            {
                Id = folder.Id,
                Name = folder.Name,
                IsFolder = true,
                ItemType = "connection-folder"
            };
            folderMap[folder.Id] = item;
        }

        foreach (var folder in store.Folders)
        {
            var item = folderMap[folder.Id];
            if (folder.ParentFolderId != null
                && folderMap.TryGetValue(folder.ParentFolderId, out var parent))
            {
                item.Parent = parent;
                parent.Children.Add(item);
            }
            else
            {
                item.Parent = connectionsRoot;
                connectionsRoot.Children.Add(item);
            }
        }

        foreach (var conn in store.Connections)
        {
            var item = new ExportTreeItem
            {
                Id = conn.Id,
                Name = conn.Name,
                IsFolder = false,
                ItemType = "connection"
            };

            if (conn.FolderId != null
                && folderMap.TryGetValue(conn.FolderId, out var folderItem))
            {
                item.Parent = folderItem;
                folderItem.Children.Add(item);
            }
            else
            {
                item.Parent = connectionsRoot;
                connectionsRoot.Children.Add(item);
            }
        }

        TreeItems.Add(connectionsRoot);
    }

    private void BuildSnippetsTree()
    {
        var snippetStorage = SnippetStorageService.Instance;
        var store = snippetStorage.Store;

        var snippetsRoot = new ExportTreeItem
        {
            Name = "Snippets",
            IsFolder = true,
            ItemType = "snippets-root"
        };

        var folderMap = new Dictionary<string, ExportTreeItem>();
        foreach (var folder in store.Folders)
        {
            var item = new ExportTreeItem
            {
                Id = folder.Id,
                Name = folder.Name,
                IsFolder = true,
                ItemType = "snippet-folder"
            };
            folderMap[folder.Id] = item;
        }

        foreach (var folder in store.Folders)
        {
            var item = folderMap[folder.Id];
            if (folder.ParentFolderId != null
                && folderMap.TryGetValue(folder.ParentFolderId, out var parent))
            {
                item.Parent = parent;
                parent.Children.Add(item);
            }
            else
            {
                item.Parent = snippetsRoot;
                snippetsRoot.Children.Add(item);
            }
        }

        foreach (var snippet in store.Snippets)
        {
            var item = new ExportTreeItem
            {
                Id = snippet.Id,
                Name = snippet.Name,
                IsFolder = false,
                ItemType = "snippet"
            };

            if (snippet.FolderId != null
                && folderMap.TryGetValue(snippet.FolderId, out var folderItem))
            {
                item.Parent = folderItem;
                folderItem.Children.Add(item);
            }
            else
            {
                item.Parent = snippetsRoot;
                snippetsRoot.Children.Add(item);
            }
        }

        TreeItems.Add(snippetsRoot);
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = new ExportOptions
        {
            IncludeConnections = IsRootSelected("connections-root"),
            IncludeSnippets = IsRootSelected("snippets-root"),
            IncludePasswords = IncludePasswords,
            IncludePrivateKeys = IncludePrivateKeys,
            SelectedConnectionIds = GetSelectedIds("connection"),
            SelectedConnectionFolderIds = GetSelectedIds("connection-folder"),
            SelectedSnippetIds = GetSelectedIds("snippet"),
            SelectedSnippetFolderIds = GetSelectedIds("snippet-folder")
        };
        Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private bool IsRootSelected(string rootType)
    {
        var root = TreeItems.FirstOrDefault(t => t.ItemType == rootType);
        return root?.IsChecked != false;
    }

    private HashSet<string> GetSelectedIds(string itemType)
    {
        var ids = new HashSet<string>();
        foreach (var root in TreeItems)
        {
            CollectSelectedIds(root, itemType, ids);
        }
        return ids;
    }

    private static void CollectSelectedIds(
        ExportTreeItem item, string itemType, HashSet<string> ids)
    {
        if (item.ItemType == itemType
            && item.IsChecked == true
            && !string.IsNullOrEmpty(item.Id))
        {
            ids.Add(item.Id);
        }

        foreach (var child in item.Children)
        {
            CollectSelectedIds(child, itemType, ids);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close(false);
        base.OnKeyDown(e);
    }
}

/// <summary>
/// Converts a bool (IsFolder) to a Material icon kind for the export tree.
/// Folders get FolderOutline, items get FileDocumentOutline.
/// </summary>
public class BoolToFolderIconConverter : IValueConverter
{
    public object Convert(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? MaterialIconKind.FolderOutline
            : MaterialIconKind.FileDocumentOutline;
    }

    public object ConvertBack(
        object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
