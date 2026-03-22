using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

public partial class SnippetsSidebarViewModel : ObservableObject
{
    private readonly SnippetStorageService _storage = SnippetStorageService.Instance;

    public ObservableCollection<CommandSnippet> Snippets { get; } = [];

    public event Action<string>? SnippetExecuteRequested;

    public SnippetsSidebarViewModel()
    {
        Reload();
    }

    public void Reload()
    {
        Snippets.Clear();
        foreach (var snippet in _storage.GetSnippets())
            Snippets.Add(snippet);
    }

    public void AddSnippet(CommandSnippet snippet)
    {
        _storage.AddSnippet(snippet);
        Snippets.Add(snippet);
    }

    public void UpdateSnippet(CommandSnippet snippet)
    {
        _storage.UpdateSnippet(snippet);

        var index = -1;
        for (int i = 0; i < Snippets.Count; i++)
        {
            if (Snippets[i].Id == snippet.Id)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
            Snippets[index] = snippet;
    }

    public void DeleteSnippet(CommandSnippet snippet)
    {
        _storage.DeleteSnippet(snippet.Id);
        Snippets.Remove(snippet);
    }

    public void ExecuteSnippet(CommandSnippet snippet)
    {
        SnippetExecuteRequested?.Invoke(snippet.Command);
    }
}
