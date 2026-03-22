using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetSterm.Views;

/// <summary>
/// Tree item ViewModel for the export dialog with tri-state checkbox logic.
/// Parent checked/unchecked propagates to all children.
/// Child changes update parent to: true (all checked), false (none checked), null (mixed).
/// </summary>
public partial class ExportTreeItem : ObservableObject
{
    private bool _suppressPropagation;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool? _isChecked = true;
    [ObservableProperty] private bool _isFolder;

    public string Id { get; set; } = "";

    /// <summary>
    /// One of: "connection", "snippet", "connection-folder", "snippet-folder",
    /// "connections-root", "snippets-root".
    /// </summary>
    public string ItemType { get; set; } = "";

    public ObservableCollection<ExportTreeItem> Children { get; } = [];
    public ExportTreeItem? Parent { get; set; }

    partial void OnIsCheckedChanged(bool? value)
    {
        if (_suppressPropagation)
            return;

        _suppressPropagation = true;
        try
        {
            if (value.HasValue)
                SetChildrenChecked(value.Value);

            Parent?.UpdateFromChildren();
        }
        finally
        {
            _suppressPropagation = false;
        }
    }

    private void SetChildrenChecked(bool isChecked)
    {
        foreach (var child in Children)
        {
            child._suppressPropagation = true;
            child.IsChecked = isChecked;
            child.SetChildrenChecked(isChecked);
            child._suppressPropagation = false;
        }
    }

    private void UpdateFromChildren()
    {
        if (Children.Count == 0)
            return;

        bool allChecked = Children.All(c => c.IsChecked == true);
        bool noneChecked = Children.All(c => c.IsChecked == false);

        _suppressPropagation = true;
        try
        {
            if (allChecked)
                IsChecked = true;
            else if (noneChecked)
                IsChecked = false;
            else
                IsChecked = null;
        }
        finally
        {
            _suppressPropagation = false;
        }

        Parent?.UpdateFromChildren();
    }
}
