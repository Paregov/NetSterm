using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetSterm.Models;

namespace NetSterm.ViewModels;

public partial class SnippetTreeItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEditing;

    public string Id { get; set; } = "";
    public bool IsFolder { get; set; }
    public CommandSnippet? Snippet { get; set; }
    public ObservableCollection<SnippetTreeItem> Children { get; } = [];
}
