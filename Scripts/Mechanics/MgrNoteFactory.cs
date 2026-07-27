namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// The single construction point for note runtime objects.
/// </summary>
public static class MgrNoteFactory
{
    public static MgrNote Create(NoteKind kind) => kind switch
    {
        NoteKind.Attack => new AttackNote(),
        NoteKind.Skill => new SkillNote(),
        NoteKind.Power => new PowerNote(),
        NoteKind.Status => new StatusNote(),
        NoteKind.Curse => new CurseNote(),
        NoteKind.Starry => new StarryNote(),
        NoteKind.Ghost => new GhostNote(),
        NoteKind.Everything => new EverythingNote(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MGR note kind.")
    };
}
