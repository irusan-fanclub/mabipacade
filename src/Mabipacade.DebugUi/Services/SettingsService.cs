using System.IO;
using System.Text.Json;
using Mabipacade.DebugUi.Models;

namespace Mabipacade.DebugUi.Services;

public sealed class SettingsService
{
    private readonly string _path;
    private readonly string _tmp;
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsService(string appDir)
    {
        _path = Path.Combine(appDir, "settings.json");
        _tmp = _path + ".tmp";
    }

    public DebugUiSettings Load()
    {
        if (!File.Exists(_path)) return new DebugUiSettings();
        try
        {
            using var fs = File.OpenRead(_path);
            return JsonSerializer.Deserialize<DebugUiSettings>(fs, Opts) ?? new DebugUiSettings();
        }
        catch { return new DebugUiSettings(); }
    }

    public void Save(DebugUiSettings s)
    {
        File.WriteAllText(_tmp, JsonSerializer.Serialize(s, Opts));
        if (File.Exists(_path)) File.Delete(_path);
        File.Move(_tmp, _path);
    }
}
