using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinSTerm.Models;

namespace WinSTerm.Services;

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
    private List<CommandSnippet> _snippets;

    public static SnippetStorageService Instance => s_instance.Value;

    private SnippetStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "WinSTerm");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "snippets.json");
        _snippets = LoadFromDisk();
    }

    public List<CommandSnippet> GetSnippets()
    {
        lock (_lock)
        {
            return new List<CommandSnippet>(_snippets);
        }
    }

    public void AddSnippet(CommandSnippet snippet)
    {
        if (snippet == null) throw new ArgumentNullException(nameof(snippet));

        lock (_lock)
        {
            _snippets.Add(snippet);
            Save();
        }
    }

    public void UpdateSnippet(CommandSnippet snippet)
    {
        if (snippet == null) throw new ArgumentNullException(nameof(snippet));

        lock (_lock)
        {
            var index = _snippets.FindIndex(s => s.Id == snippet.Id);
            if (index >= 0)
            {
                _snippets[index] = snippet;
                Save();
            }
        }
    }

    public void DeleteSnippet(string snippetId)
    {
        lock (_lock)
        {
            _snippets.RemoveAll(s => s.Id == snippetId);
            Save();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_snippets, s_jsonOptions);
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private List<CommandSnippet> LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<CommandSnippet>>(json, s_jsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
