using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetSterm.Models;

namespace NetSterm.ViewModels;

public partial class SessionTreeItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEditing;

    public string Id { get; set; } = "";
    public bool IsFolder { get; set; }
    public ConnectionInfo? ConnectionInfo { get; set; }
    public ObservableCollection<SessionTreeItem> Children { get; } = new();

    public string IconKind => IsFolder ? "Folder" : "Console";
}
