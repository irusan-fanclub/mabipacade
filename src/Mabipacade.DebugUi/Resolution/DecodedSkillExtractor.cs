using System.Reflection;

namespace Mabipacade.DebugUi.Resolution;

public static class DecodedSkillExtractor
{
    public static IReadOnlyList<int> ExtractSkillIds(object? decoded)
    {
        if (decoded is null) return Array.Empty<int>();

        // PlayerSkill* records have a single ushort SkillId property.
        // CombatActionPack has a Sub list whose elements have SkillId + SubSkillId.
        // Use reflection so we don't have to hard-code each decoder type.
        var ids = new List<int>();
        ExtractInto(decoded, ids);
        return ids;
    }

    private static void ExtractInto(object value, List<int> ids)
    {
        switch (value)
        {
            case null:
                return;
            case System.Collections.IEnumerable enumerable when value is not string:
                foreach (var item in enumerable)
                {
                    if (item is not null) ExtractInto(item, ids);
                }
                return;
        }

        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)) return;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Match property names ending with "SkillId" (covers SkillId, SubSkillId).
            if (!prop.Name.EndsWith("SkillId", StringComparison.Ordinal)) continue;
            var propValue = prop.GetValue(value);
            if (propValue is null) continue;
            int? asInt = propValue switch
            {
                byte b => b,
                ushort us => us,
                short s => s,
                int i => i,
                uint u when u <= int.MaxValue => (int)u,
                long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
                _ => null,
            };
            if (asInt is int x && x > 0) ids.Add(x);
        }

        // Recurse into List<>/IReadOnlyList<> properties (e.g., CombatActionPack.Sub).
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType == typeof(string)) continue;
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType)) continue;
            var enumerable = prop.GetValue(value) as System.Collections.IEnumerable;
            if (enumerable is null) continue;
            foreach (var item in enumerable)
            {
                if (item is not null) ExtractInto(item, ids);
            }
        }
    }
}
