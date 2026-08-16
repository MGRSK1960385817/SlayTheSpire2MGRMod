using MegaCrit.Sts2.Core.Models;

namespace MGRMod.Mechanics;

/// <summary>
/// One card in the ordered performance sequence. The initial and remaining
/// counts are intentionally separate values.
/// </summary>
public sealed class MgrPerformanceEntry
{
    public MgrPerformanceEntry(CardModel card, int initialPerformanceTurns)
        : this(card, initialPerformanceTurns, initialPerformanceTurns)
    {
    }

    private MgrPerformanceEntry(
        CardModel card,
        int initialPerformanceTurns,
        int remainingPerformanceTurns)
    {
        Card = card;
        InitialPerformanceTurns = initialPerformanceTurns;
        RemainingPerformanceTurns = remainingPerformanceTurns;
    }

    public CardModel Card { get; }

    public int InitialPerformanceTurns { get; private set; }

    public int RemainingPerformanceTurns { get; private set; }

    public void ConsumeOnePerformance()
    {
        if (RemainingPerformanceTurns > 0)
            RemainingPerformanceTurns--;
    }

    public void ResetRemainingTurns()
    {
        RemainingPerformanceTurns = InitialPerformanceTurns;
    }

    public void AddPerformanceTurns(int amount)
    {
        if (amount <= 0)
            return;

        InitialPerformanceTurns = checked(InitialPerformanceTurns + amount);
        RemainingPerformanceTurns = checked(RemainingPerformanceTurns + amount);
    }

    /// <summary>
    /// Creates a new physical card entry in the same queue slot. Counters are
    /// deliberately initialized from the incoming card's assigned value and
    /// never inherited from the outgoing card.
    /// </summary>
    public MgrPerformanceEntry CreateReplacement(
        CardModel card,
        int replacementPerformanceTurns)
    {
        int turns = Math.Max(1, replacementPerformanceTurns);
        return new MgrPerformanceEntry(card, turns, turns);
    }
}
