using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using SlayTheSpire2MGRMod.Cards;

namespace SlayTheSpire2MGRMod.Mechanics;

[Flags]
public enum MgrKeywordKind
{
    None = 0,
    Forte = 1 << 0,
    PhraseStart = 1 << 1,
    PhraseEnd = 1 << 2,
    Performance = 1 << 3,
    Starry = 1 << 4,
    BasicNotes = 1 << 5,
    AttackNote = 1 << 6,
    SkillNote = 1 << 7,
    PowerNote = 1 << 8,
    StatusNote = 1 << 9,
    CurseNote = 1 << 10,
    StarryNote = 1 << 12,
    GhostNote = 1 << 13,
    Chord = 1 << 14,
    Everything = 1 << 15
}

/// <summary>
/// One authoritative vocabulary for MGR card text and hover tips. Registered
/// keywords ride Tower 2's native CardKeyword collection, so cloning, upgrades,
/// combat saves and card previews all use the same path as vanilla keywords.
/// </summary>
public static class MgrKeywords
{
    public const string ForteKey = "forte";
    public const string PhraseStartKey = "phrase_start";
    public const string PhraseEndKey = "phrase_end";
    public const string PerformanceKey = "performance";
    public const string StarryKey = "starry";
    public const string BasicNotesKey = "basic_notes";
    public const string AttackNoteKey = "attack_note";
    public const string SkillNoteKey = "skill_note";
    public const string PowerNoteKey = "power_note";
    public const string StatusNoteKey = "status_note";
    public const string CurseNoteKey = "curse_note";
    public const string StarryNoteKey = "starry_note";
    public const string GhostNoteKey = "ghost_note";
    public const string ChordKey = "chord";
    public const string EverythingKey = "everything";

    public static readonly string Forte = Qualify(ForteKey);
    public static readonly string PhraseStart = Qualify(PhraseStartKey);
    public static readonly string PhraseEnd = Qualify(PhraseEndKey);
    public static readonly string Performance = Qualify(PerformanceKey);
    public static readonly string Starry = Qualify(StarryKey);
    public static readonly string BasicNotes = Qualify(BasicNotesKey);
    public static readonly string AttackNote = Qualify(AttackNoteKey);
    public static readonly string SkillNote = Qualify(SkillNoteKey);
    public static readonly string PowerNote = Qualify(PowerNoteKey);
    public static readonly string StatusNote = Qualify(StatusNoteKey);
    public static readonly string CurseNote = Qualify(CurseNoteKey);
    public static readonly string StarryNote = Qualify(StarryNoteKey);
    public static readonly string GhostNote = Qualify(GhostNoteKey);
    public static readonly string Chord = Qualify(ChordKey);
    public static readonly string Everything = Qualify(EverythingKey);

    public static CardKeyword PerformanceKeyword => Performance.GetModCardKeyword();

    public static IEnumerable<string> GetIds(MgrCard card)
    {
        MgrKeywordKind kinds = card.DeclaredKeywordKinds | InferKinds(card);

        if (card.InitialPerformanceTurns > 0)
            kinds |= MgrKeywordKind.Performance;

        if (card.IsStarryCard)
            kinds |= MgrKeywordKind.Starry | MgrKeywordKind.StarryNote;

        kinds |= card.NoteOverride switch
        {
            NoteKind.Attack => MgrKeywordKind.AttackNote,
            NoteKind.Skill => MgrKeywordKind.SkillNote,
            NoteKind.Power => MgrKeywordKind.PowerNote,
            NoteKind.Status => MgrKeywordKind.StatusNote,
            NoteKind.Curse => MgrKeywordKind.CurseNote,
            NoteKind.Starry => MgrKeywordKind.StarryNote,
            NoteKind.Ghost => MgrKeywordKind.GhostNote,
            NoteKind.Everything => MgrKeywordKind.Everything,
            _ => MgrKeywordKind.None
        };

        MgrGoldGlowCondition glow = card.DeclaredGoldGlowConditions;
        if (glow.HasFlag(MgrGoldGlowCondition.PhraseStart))
            kinds |= MgrKeywordKind.PhraseStart;
        if (glow.HasFlag(MgrGoldGlowCondition.PhraseEnd))
            kinds |= MgrKeywordKind.PhraseEnd;
        if (glow.HasFlag(MgrGoldGlowCondition.ChordResolvedThisTurn) ||
            glow.HasFlag(MgrGoldGlowCondition.NoChordResolvedThisTurn))
        {
            kinds |= MgrKeywordKind.Chord;
        }

        foreach ((MgrKeywordKind kind, string id) in OrderedKeywords)
        {
            if (kinds.HasFlag(kind))
                yield return id;
        }
    }

    private static string Qualify(string stem) =>
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, stem);

    /// <summary>
    /// Central declaration for concepts explicitly named by current card text.
    /// Keeping this in one place avoids scattering UI-only hover metadata through
    /// effect code. A future card with unusual wording may still override
    /// MgrCard.KeywordKinds directly.
    /// </summary>
    private static MgrKeywordKind InferKinds(MgrCard card) => card switch
    {
        SongOfBeginning => MgrKeywordKind.AttackNote | MgrKeywordKind.SkillNote,
        LightSong => MgrKeywordKind.BasicNotes |
                     MgrKeywordKind.StarryNote |
                     MgrKeywordKind.GhostNote,
        BroomStrike => MgrKeywordKind.AttackNote,
        Strafe => MgrKeywordKind.AttackNote,
        SatelliteGirl => MgrKeywordKind.Chord,
        MasterSpark => MgrKeywordKind.Forte,
        MaguroAssault => MgrKeywordKind.Chord,
        StageWarmUp => MgrKeywordKind.Forte,
        OtomeDissection => MgrKeywordKind.Forte,
        StainedNocturne => MgrKeywordKind.CurseNote,
        DaybreakFrontline => MgrKeywordKind.CurseNote | MgrKeywordKind.StatusNote,
        CumulonimbusGraffiti => MgrKeywordKind.Chord,
        ShowWeakness => MgrKeywordKind.SkillNote,
        LittleMiracles => MgrKeywordKind.BasicNotes,
        Resonate => MgrKeywordKind.Performance,
        EastOfTimeline => MgrKeywordKind.AttackNote,
        HarmonyForm => MgrKeywordKind.Chord,
        Stereophonic => MgrKeywordKind.AttackNote |
                       MgrKeywordKind.SkillNote |
                       MgrKeywordKind.PowerNote,
        Higan => MgrKeywordKind.Forte |
                 MgrKeywordKind.PhraseStart |
                 MgrKeywordKind.PhraseEnd,
        SpringStorm => MgrKeywordKind.Forte,
        InfernoLoveLetter => MgrKeywordKind.AttackNote,
        Adios => MgrKeywordKind.Performance,
        Encore => MgrKeywordKind.Performance,
        MindMirage => MgrKeywordKind.PowerNote,
        Chorus => MgrKeywordKind.BasicNotes,
        Futariboshi => MgrKeywordKind.StarryNote,
        Unison => MgrKeywordKind.BasicNotes,
        DelusionalSketch => MgrKeywordKind.Performance,
        DualLovers => MgrKeywordKind.Performance | MgrKeywordKind.AttackNote,
        CowardRocket => MgrKeywordKind.Performance,
        LastSinger => MgrKeywordKind.Performance,
        UniverseOf88Keys => MgrKeywordKind.Chord,
        CubicPrism => MgrKeywordKind.Performance,
        GalaxyLamp => MgrKeywordKind.StarryNote,
        TheCrowd => MgrKeywordKind.BasicNotes,
        TheCrowdChoice => MgrKeywordKind.BasicNotes,
        SongOfEverything => MgrKeywordKind.Everything,
        _ => MgrKeywordKind.None
    };

    private static readonly (MgrKeywordKind Kind, string Id)[] OrderedKeywords =
    [
        (MgrKeywordKind.Starry, Starry),
        (MgrKeywordKind.Performance, Performance),
        (MgrKeywordKind.Forte, Forte),
        (MgrKeywordKind.PhraseStart, PhraseStart),
        (MgrKeywordKind.PhraseEnd, PhraseEnd),
        (MgrKeywordKind.Chord, Chord),
        (MgrKeywordKind.Everything, Everything),
        (MgrKeywordKind.BasicNotes, BasicNotes),
        (MgrKeywordKind.AttackNote, AttackNote),
        (MgrKeywordKind.SkillNote, SkillNote),
        (MgrKeywordKind.PowerNote, PowerNote),
        (MgrKeywordKind.StatusNote, StatusNote),
        (MgrKeywordKind.CurseNote, CurseNote),
        (MgrKeywordKind.StarryNote, StarryNote),
        (MgrKeywordKind.GhostNote, GhostNote)
    ];
}

/// <summary>
/// Attribute-only registration host discovered by RitsuLib at model startup.
/// Placement remains None because MGR controls line order and Starry's purple
/// styling itself; the registered values still produce native right-side tips.
/// </summary>
internal static class MgrKeywordRegistration
{
    [RegisterOwnedCardKeyword(MgrKeywords.ForteKey,
        IconPath = $"res://{Entry.ModId}/images/powers/Forte.png")]
    private sealed class Forte;

    [RegisterOwnedCardKeyword(MgrKeywords.PhraseStartKey)]
    private sealed class PhraseStart;

    [RegisterOwnedCardKeyword(MgrKeywords.PhraseEndKey)]
    private sealed class PhraseEnd;

    [RegisterOwnedCardKeyword(MgrKeywords.PerformanceKey)]
    private sealed class Performance;

    [RegisterOwnedCardKeyword(MgrKeywords.StarryKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Starry.png")]
    private sealed class Starry;

    [RegisterOwnedCardKeyword(MgrKeywords.BasicNotesKey,
        IconPath = $"res://{Entry.ModId}/images/notes/BasicNotes.png")]
    private sealed class BasicNotes;

    [RegisterOwnedCardKeyword(MgrKeywords.AttackNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Attack.png")]
    private sealed class AttackNote;

    [RegisterOwnedCardKeyword(MgrKeywords.SkillNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Skill.png")]
    private sealed class SkillNote;

    [RegisterOwnedCardKeyword(MgrKeywords.PowerNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Power.png")]
    private sealed class PowerNote;

    [RegisterOwnedCardKeyword(MgrKeywords.StatusNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Status.png")]
    private sealed class StatusNote;

    [RegisterOwnedCardKeyword(MgrKeywords.CurseNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Curse.png")]
    private sealed class CurseNote;

    [RegisterOwnedCardKeyword(MgrKeywords.StarryNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Starry.png")]
    private sealed class StarryNote;

    [RegisterOwnedCardKeyword(MgrKeywords.GhostNoteKey,
        IconPath = $"res://{Entry.ModId}/images/notes/Ghost.png")]
    private sealed class GhostNote;

    [RegisterOwnedCardKeyword(MgrKeywords.ChordKey,
        IconPath = $"res://{Entry.ModId}/images/notes/BasicNotes.png")]
    private sealed class Chord;

    [RegisterOwnedCardKeyword(MgrKeywords.EverythingKey,
        IconPath = $"res://{Entry.ModId}/images/notes/BasicNotes.png")]
    private sealed class Everything;
}
