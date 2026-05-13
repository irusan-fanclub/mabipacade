using System.IO;
using Mabipacade.DebugUi.Models;
using Mabipacade.DebugUi.Resolution;

namespace Mabipacade.DebugUi.Services;

public sealed class NameResolverService
{
    private NameResolver _current = NameResolver.Empty;
    private readonly object _lock = new();

    public NameResolver Current
    {
        get { lock (_lock) return _current; }
    }

    public event EventHandler? Changed;

    public void Reload(DebugUiSettings settings)
    {
        var dir = settings.XmlDataDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            lock (_lock) _current = NameResolver.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        var (xmlPath, txtPath) = ResolvePaths(dir);
        try
        {
            var skills = SkillInfoLoader.Load(xmlPath, txtPath);
            lock (_lock) _current = new NameResolver(skills);
        }
        catch
        {
            lock (_lock) _current = NameResolver.Empty;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Supports two layouts:
    //   flat:      <dir>/SkillInfo.xml + <dir>/SkillInfo.taiwan.txt
    //   extracted: <dir>/data/db/Skill/SkillInfo.xml + <dir>/data/local/xml/SkillInfo.taiwan.txt
    // Returns empty paths if neither layout matches — Load will then return Empty.
    internal static (string xml, string txt) ResolvePaths(string dir)
    {
        var flatXml = Path.Combine(dir, "SkillInfo.xml");
        var flatTxt = Path.Combine(dir, "SkillInfo.taiwan.txt");
        if (File.Exists(flatXml))
            return (flatXml, flatTxt);

        var extractedXml = Path.Combine(dir, "data", "db", "Skill", "SkillInfo.xml");
        var extractedTxt = Path.Combine(dir, "data", "local", "xml", "SkillInfo.taiwan.txt");
        if (File.Exists(extractedXml))
            return (extractedXml, extractedTxt);

        return (flatXml, flatTxt);   // both missing — SkillInfoLoader.Load returns Empty
    }
}
