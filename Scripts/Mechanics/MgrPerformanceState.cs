using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Combat-only ordered performance sequence for one player.
/// Entry zero is always the earliest card and is rendered on the right.
/// </summary>
public sealed class MgrPerformanceState
{
    private readonly List<MgrPerformanceEntry> _entries = [];

    public IReadOnlyList<MgrPerformanceEntry> Entries => _entries;
    public int PlayedEntriesQueuedThisTurn { get; private set; }
    public int PerformanceCardsPlayedThisCombat { get; private set; }

    public bool Contains(CardModel card) => _entries.Any(entry => ReferenceEquals(entry.Card, card));

    public MgrPerformanceEntry? Enqueue(
        CardModel card,
        int initialPerformanceTurns,
        int bonusPerformances = 0)
    {
        if (Contains(card) || initialPerformanceTurns <= 0)
            return null;

        int initialTurns = Math.Max(1, initialPerformanceTurns + bonusPerformances);
        var entry = new MgrPerformanceEntry(card, initialTurns);
        _entries.Add(entry);
        return entry;
    }

    public bool Remove(MgrPerformanceEntry entry) => _entries.Remove(entry);

    public int RecordPlayedEntryQueuedThisTurn()
    {
        int previous = PlayedEntriesQueuedThisTurn;
        PlayedEntriesQueuedThisTurn++;
        return previous;
    }

    public void RecordPerformanceCardPlayed() =>
        PerformanceCardsPlayedThisCombat = checked(PerformanceCardsPlayedThisCombat + 1);

    public void ResetTurnCounters() => PlayedEntriesQueuedThisTurn = 0;

    public void Clear()
    {
        _entries.Clear();
        PlayedEntriesQueuedThisTurn = 0;
        PerformanceCardsPlayedThisCombat = 0;
    }
}

public static class MgrPerformanceStateStore
{
    private static readonly Dictionary<Player, MgrPerformanceState> States = [];

    public static MgrPerformanceState For(Player player)
    {
        if (!States.TryGetValue(player, out MgrPerformanceState? state))
        {
            state = new MgrPerformanceState();
            States[player] = state;
        }

        return state;
    }

    public static bool TryGet(Player player, out MgrPerformanceState state) =>
        States.TryGetValue(player, out state!);

    public static void Clear() => States.Clear();
}
