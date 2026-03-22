using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetSterm.Models;

namespace NetSterm.Services;

public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> s_instance = new(() => new SettingsService());

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly object _lock = new();

    public static SettingsService Instance => s_instance.Value;

    public AppSettings Current { get; private set; }

    public event Action? SettingsChanged;

    private SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "NetSterm");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Current = LoadFromDisk();
    }

    public void Reload()
    {
        lock (_lock)
        {
            Current = LoadFromDisk();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(Current, s_jsonOptions);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }

        SettingsChanged?.Invoke();
    }

    public void Apply(AppSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        lock (_lock)
        {
            Current = settings;
        }

        Save();
    }

    private AppSettings LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, s_jsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
