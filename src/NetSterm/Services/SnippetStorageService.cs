using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetSterm.Models;

namespace NetSterm.Services;

public sealed class SnippetStorageService
{
    private static readonly Lazy<SnippetStorageService> s_instance = new(() => new SnippetStorageService());

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly object _lock = new();
    private SnippetStore _store;

    public static SnippetStorageService Instance => s_instance.Value;

    public SnippetStore Store => _store;

    private SnippetStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "NetSterm");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "snippets.json");
        _store = LoadFromDisk();
    }

    public List<CommandSnippet> GetSnippets()
    {
        lock (_lock)
        {
            return new List<CommandSnippet>(_store.Snippets);
        }
    }

    public void AddSnippet(CommandSnippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        lock (_lock)
        {
            _store.Snippets.Add(snippet);
            Save();
        }
    }

    public void UpdateSnippet(CommandSnippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        lock (_lock)
        {
            var index = _store.Snippets.FindIndex(s => s.Id == snippet.Id);
            if (index >= 0)
            {
                _store.Snippets[index] = snippet;
                Save();
            }
        }
    }

    public void DeleteSnippet(string snippetId)
    {
        lock (_lock)
        {
            _store.Snippets.RemoveAll(s => s.Id == snippetId);
            Save();
        }
    }

    public void AddFolder(SnippetFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        lock (_lock)
        {
            _store.Folders.Add(folder);
            Save();
        }
    }

    public void DeleteFolder(string folderId)
    {
        lock (_lock)
        {
            DeleteFolderRecursive(folderId);
            Save();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_store, s_jsonOptions);
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private void DeleteFolderRecursive(string folderId)
    {
        _store.Snippets.RemoveAll(s => s.FolderId == folderId);

        var subFolders = _store.Folders.Where(f => f.ParentFolderId == folderId).ToList();
        foreach (var sub in subFolders)
            DeleteFolderRecursive(sub.Id);

        _store.Folders.RemoveAll(f => f.Id == folderId);
    }

    private SnippetStore LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return new SnippetStore();

        try
        {
            var json = File.ReadAllText(_filePath);

            return JsonSerializer.Deserialize<SnippetStore>(json, s_jsonOptions) ?? new SnippetStore();
        }
        catch
        {
            return new SnippetStore();
        }
    }
}
