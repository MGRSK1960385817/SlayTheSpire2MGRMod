using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Utils;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Per-player, per-combat MGR mechanic state.
/// </summary>
public sealed class MgrCombatState
{
    public PhraseState Phrase { get; } = new();
    public int TotalNotesGenerated { get; private set; }
    public int ChordsResolvedThisCombat { get; private set; }
    public int ChordsResolvedThisTurn { get; private set; }
    public int Forte { get; private set; }
    public PhraseResolution? LastResolution { get; private set; }

    /// <summary>
    /// Adds one note and immediately resolves a completed phrase.
    /// </summary>
    public PhraseResolution? AddNote(NoteKind kind)
    {
        return AddNote(MgrNoteFactory.Create(kind));
    }

    public PhraseResolution? AddNote(MgrNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        Phrase.Add(note);
        TotalNotesGenerated++;

        LastResolution = Phrase.IsComplete ? ResolveCompletedPhrase() : null;

        return LastResolution;
    }

    /// <summary>
    /// Changes the live slot count. If reducing it makes one or more phrases
    /// complete, those resolutions are returned in their original note order.
    /// </summary>
    public IReadOnlyList<PhraseResolution> SetPhraseCapacity(int capacity)
    {
        Phrase.SetCapacity(capacity);
        LastResolution = null;

        List<PhraseResolution> resolutions = [];
        while (Phrase.IsComplete)
            resolutions.Add(ResolveCompletedPhrase());

        return resolutions;
    }

    private PhraseResolution ResolveCompletedPhrase()
    {
        PhraseResolution resolution = Phrase.Resolve();
        LastResolution = resolution;
        ChordsResolvedThisCombat++;
        ChordsResolvedThisTurn++;
        return resolution;
    }

    public void SetForteSnapshot(int amount)
    {
        Forte = Math.Clamp(amount, -999, 999);
    }

    public void ResetTurnCounters()
    {
        ChordsResolvedThisTurn = 0;
    }
}

public static class MgrCombatStateStore
{
    private static readonly AttachedState<Player, MgrCombatState> States = new(() => new MgrCombatState());

    public static MgrCombatState For(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return States[player];
    }

    public static bool TryGet(Player player, out MgrCombatState state)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (States.TryGetValue(player, out MgrCombatState? found))
        {
            state = found;
            return true;
        }

        state = null!;
        return false;
    }

    public static void Clear() => States.Clear();
}
