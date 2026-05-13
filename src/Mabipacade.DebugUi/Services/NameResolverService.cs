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

        var xmlPath = Path.Combine(dir, "SkillInfo.xml");
        var txtPath = Path.Combine(dir, "SkillInfo.taiwan.txt");
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
}
