using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// One card in the ordered performance sequence. The initial and remaining
/// counts are intentionally separate values.
/// </summary>
public sealed class MgrPerformanceEntry
{
    public MgrPerformanceEntry(CardModel card, int initialPerformanceTurns)
    {
        Card = card;
        InitialPerformanceTurns = initialPerformanceTurns;
        RemainingPerformanceTurns = initialPerformanceTurns;
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
}
