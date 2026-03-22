using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WinSTerm.Models;

namespace WinSTerm.ViewModels;

public partial class SessionTreeItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;

    public string Id { get; set; } = "";
    public bool IsFolder { get; set; }
    public ConnectionInfo? ConnectionInfo { get; set; }
    public ObservableCollection<SessionTreeItem> Children { get; } = new();

    public string IconKind => IsFolder ? "Folder" : "Console";
}
