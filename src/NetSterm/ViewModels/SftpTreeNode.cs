using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetSterm.ViewModels;

public partial class SftpTreeNode : ObservableObject
{
    internal const string DummySentinel = "__dummy__";

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _fullPath = "";
    [ObservableProperty] private bool _isDirectory;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private long _size;

    public ObservableCollection<SftpTreeNode> Children { get; } = [];

    public bool HasDummyChild => Children.Count == 1 && Children[0].FullPath == DummySentinel;

    public string SizeDisplay => IsDirectory ? "" : FormatSize(Size);

    public event EventHandler? ExpandRequested;

    public static SftpTreeNode CreateDirectory(string name, string fullPath)
    {
        var node = new SftpTreeNode
        {
            Name = name,
            FullPath = fullPath,
            IsDirectory = true
        };
        node.Children.Add(new SftpTreeNode { Name = "Loading\u2026", FullPath = DummySentinel });
        return node;
    }

    public static SftpTreeNode CreateFile(string name, string fullPath, long size)
    {
        return new SftpTreeNode
        {
            Name = name,
            FullPath = fullPath,
            IsDirectory = false,
            Size = size
        };
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && HasDummyChild)
            ExpandRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
