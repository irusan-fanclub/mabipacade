using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Mabipacade.DebugUi.Resolution;

public static class SkillInfoLoader
{
    public static IReadOnlyDictionary<int, SkillNameEntry> Load(string xmlPath, string localizationTxtPath)
    {
        var localization = LoadLocalization(localizationTxtPath);
        return ParseSkillXml(xmlPath, localization);
    }

    public static IReadOnlyDictionary<string, string> LoadLocalization(string txtPath)
    {
        if (!File.Exists(txtPath)) return new Dictionary<string, string>();
        var result = new Dictionary<string, string>();
        using var sr = new StreamReader(txtPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int tab = line.IndexOf('\t');
            if (tab < 0) continue;
            var key = line[..tab];
            var value = line[(tab + 1)..];
            result[key] = value;
        }
        return result;
    }

    private static readonly Regex LocalNameRefRe = new(@"_LT\[(xml\.skillinfo\.\d+)\]", RegexOptions.Compiled);

    public static IReadOnlyDictionary<int, SkillNameEntry> ParseSkillXml(string xmlPath, IReadOnlyDictionary<string, string> localization)
    {
        if (!File.Exists(xmlPath)) return new Dictionary<int, SkillNameEntry>();
        var result = new Dictionary<int, SkillNameEntry>();
        using var sr = new StreamReader(xmlPath, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (!line.Contains("<Skill ", StringComparison.Ordinal)) continue;
            var idMatch = Regex.Match(line, @"SkillID='(\d+)'");
            if (!idMatch.Success) continue;
            int id = int.Parse(idMatch.Groups[1].Value);
            string english = Regex.Match(line, @"SkillEngName='([^']*)'").Groups[1].Value;
            string localRef = Regex.Match(line, @"SkillLocalName='([^']*)'").Groups[1].Value;
            string localName = ResolveLocalName(localRef, localization);
            // Last-occurrence wins (file order). This trap is documented in
            // mabinogi-it-modding notes section "trap 4".
            result[id] = new SkillNameEntry(id, english, localName);
        }
        return result;
    }

    private static string ResolveLocalName(string raw, IReadOnlyDictionary<string, string> localization)
    {
        var m = LocalNameRefRe.Match(raw);
        if (!m.Success) return raw;
        return localization.TryGetValue(m.Groups[1].Value, out var v) ? v : raw;
    }
}
