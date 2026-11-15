using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;
using MGRMod.Cards;

namespace MGRMod.Mechanics;

/// <summary>
/// Combat-only additions to a card's printed Performance value. This mirrors
/// Hidden Gem changing BaseReplayCount on the live combat card, but remains a
/// separate MGR mechanic and is cleared when combat ends.
/// </summary>
public static class MgrPerformanceModifierState
{
    private static readonly Dictionary<CardModel, int> AdditionalPerformances = [];
    private static readonly Dictionary<CardModel, int> DirectPerformanceDeltas = [];
    private static readonly HashSet<CardModel> PerformanceKeywordAddedByModifier = [];

    public static int GetAdditionalPerformances(CardModel card) =>
        AdditionalPerformances.GetValueOrDefault(card);

    public static int Grant(CardModel card, int amount)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (amount <= 0)
            return GetAdditionalPerformances(card);

        // Native MGR Performance cards already render a Performance DynamicVar.
        // Modifying the live combat value updates the existing line instead of
        // appending a second, contradictory line. The delta is restored later.
        if (card is MgrCard { InitialPerformanceTurns: > 0 } &&
            card.DynamicVars.TryGetValue("Performance", out var performanceVar))
        {
            performanceVar.BaseValue += amount;
            DirectPerformanceDeltas[card] = checked(
                (DirectPerformanceDeltas.TryGetValue(card, out int delta) ? delta : 0) + amount);
            return performanceVar.IntValue;
        }

        AdditionalPerformances[card] = checked(AdditionalPerformances.GetValueOrDefault(card) + amount);
        RefreshPerformanceKeyword(card);
        return GetAdditionalPerformances(card);
    }

    private static void RefreshPerformanceKeyword(CardModel card)
    {
        if (GetAdditionalPerformances(card) > 0)
        {
            if (!card.Keywords.Contains(MgrKeywords.PerformanceKeyword))
            {
                card.AddModKeyword(MgrKeywords.PerformanceKeyword);
                PerformanceKeywordAddedByModifier.Add(card);
            }
        }
        else if (PerformanceKeywordAddedByModifier.Remove(card))
        {
            card.RemoveModKeyword(MgrKeywords.PerformanceKeyword);
        }
    }

    public static void Clear()
    {
        foreach ((CardModel card, int delta) in DirectPerformanceDeltas)
        {
            if (card.DynamicVars.TryGetValue("Performance", out var performanceVar))
                performanceVar.BaseValue -= delta;
        }

        DirectPerformanceDeltas.Clear();
        AdditionalPerformances.Clear();

        foreach (CardModel card in PerformanceKeywordAddedByModifier)
        {
            if (card.IsMutable)
                card.RemoveModKeyword(MgrKeywords.PerformanceKeyword);
        }
        PerformanceKeywordAddedByModifier.Clear();
    }
}
