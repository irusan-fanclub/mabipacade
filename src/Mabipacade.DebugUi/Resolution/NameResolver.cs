namespace Mabipacade.DebugUi.Resolution;

public sealed class NameResolver
{
    private readonly IReadOnlyDictionary<int, SkillNameEntry> _skills;

    public NameResolver(IReadOnlyDictionary<int, SkillNameEntry> skills)
    {
        _skills = skills;
    }

    public static NameResolver Empty { get; } = new(new Dictionary<int, SkillNameEntry>());

    public string? TryResolveSkill(int skillId) =>
        _skills.TryGetValue(skillId, out var e) ? e.LocalName : null;

    public bool TryResolveSkillFull(int skillId, out SkillNameEntry entry) =>
        _skills.TryGetValue(skillId, out entry!);

    public int SkillCount => _skills.Count;
}
