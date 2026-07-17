using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Relics;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Centralized scheduler for the MGR Performance mechanic. A stable snapshot is
/// processed each turn so effects that enqueue new cards cannot disturb the
/// current pass or reorder older entries.
/// </summary>
public static class MgrPerformanceSystem
{
    private static readonly HashSet<CardModel> CompletingCards = [];

    public static void ClearAll()
    {
        CompletingCards.Clear();
        MgrPerformanceVisuals.ClearAll();
        MgrPerformanceStateStore.Clear();
        MgrPerformanceModifierState.Clear();
    }

    public static int GetInitialPerformanceTurns(CardModel card)
    {
        int printed = card is MgrCard mgrCard ? mgrCard.InitialPerformanceTurns : 0;
        return Math.Max(0, printed + MgrPerformanceModifierState.GetAdditionalPerformances(card));
    }

    public static bool IsPerformanceCard(CardModel card) =>
        GetInitialPerformanceTurns(card) > 0;

    /// <summary>
    /// The final automatic play must use Tower 2's ordinary result-pile path.
    /// Earlier plays remain held in Play, while this marker lets the last play
    /// naturally reach Discard/Exhaust (or leave combat for ordinary Powers).
    /// </summary>
    public static bool IsCompletingPerformance(CardModel card) =>
        CompletingCards.Contains(card);

    public static int GrantAdditionalPerformances(CardModel card, int amount)
    {
        int total = MgrPerformanceModifierState.Grant(card, amount);
        NPlayerHand.Instance?.GetCard(card)?.UpdateVisuals(
            PileType.Hand,
            CardPreviewMode.Normal);
        return total;
    }

    public static int AddPerformancesToQueuedCards(Player player, int amount)
    {
        if (amount <= 0 ||
            !MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state))
        {
            return 0;
        }

        foreach (MgrPerformanceEntry entry in state.Entries)
        {
            MgrPerformanceModifierState.Grant(entry.Card, amount);
            entry.AddPerformanceTurns(amount);
        }

        MgrPerformanceVisuals.Show(player, state.Entries);
        return state.Entries.Count;
    }

    /// <summary>
    /// Plays every queued card once in entry order without consuming any of its
    /// remaining Performance count. The cards use the same real autoplay path as
    /// turn-start performances, so card-play hooks and note generation still fire.
    /// </summary>
    public static async Task TriggerQueuedCardsOnce(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return;
        }

        foreach (MgrPerformanceEntry entry in state.Entries.ToArray())
        {
            if (!state.Entries.Contains(entry))
                continue;

            await MgrPerformanceVisuals.PlayTriggerAnimation(player, entry);
            await CardCmd.AutoPlay(
                choiceContext,
                entry.Card,
                target: null,
                skipCardPileVisuals: true);
        }

        MgrPerformanceVisuals.Show(player, state.Entries);
    }

    public static void ObserveResolvedCardPlay(CardPlay cardPlay)
    {
        if (!cardPlay.IsLastInSeries)
            return;

        CardModel card = cardPlay.Card;
        MgrPerformanceState state = MgrPerformanceStateStore.For(card.Owner);
        int initialPerformanceTurns = GetInitialPerformanceTurns(card);
        if (state.Contains(card) || initialPerformanceTurns <= 0)
            return;

        int bonusPerformances = 0;
        if (!cardPlay.IsAutoPlay &&
            card.Owner.GetRelic<EncoreStage>() is { } encoreStage &&
            encoreStage.TryGrantPerformanceBonus())
        {
            bonusPerformances = 1;
        }

        MgrPerformanceEntry? entry = state.Enqueue(
            card,
            initialPerformanceTurns,
            bonusPerformances);
        if (entry is null)
            return;

        // The normal play that entered the sequence does not consume a turn.
        MgrPerformanceVisuals.Show(card.Owner, state.Entries);
        MgrPerformanceVisuals.QueueEntryAnimation(card.Owner, entry);
    }

    public static async Task PerformAtTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return;
        }

        MgrPerformanceEntry[] turnOrder = state.Entries.ToArray();
        foreach (MgrPerformanceEntry entry in turnOrder)
        {
            if (!state.Entries.Contains(entry))
                continue;

            bool isOrdinaryPower = entry.Card.Type == CardType.Power && !entry.Card.IsDupe;
            bool willExhaust = !isOrdinaryPower &&
                (entry.Card.Keywords.Contains(CardKeyword.Exhaust) ||
                 entry.Card.ExhaustOnNextPlay);
            bool isFinalPerformance = entry.RemainingPerformanceTurns <= 1;

            await MgrPerformanceVisuals.PlayTriggerAnimation(player, entry);

            // This is a real card play. It therefore runs every standard hook,
            // including the MGR global note-generation hook. Tower 2's central
            // autoplay presentation is skipped because the rack supplies a
            // compact in-place pulse instead.
            if (isFinalPerformance)
                CompletingCards.Add(entry.Card);

            try
            {
                await CardCmd.AutoPlay(
                    choiceContext,
                    entry.Card,
                    target: null,
                    skipCardPileVisuals: true);
            }
            finally
            {
                CompletingCards.Remove(entry.Card);
            }

            entry.ConsumeOnePerformance();

            if (entry.RemainingPerformanceTurns > 0)
            {
                MgrPerformanceVisuals.Show(player, state.Entries);
                continue;
            }

            if (entry.Card is MgrCard mgrCard)
            {
                await mgrCard.OnPerformanceFinished(
                    choiceContext,
                    new PerformanceCompletionContext(
                        player,
                        entry.InitialPerformanceTurns,
                        willExhaust));
            }

            // The final autoplay has already used the engine's normal result
            // routing, so its CardAddFinished/CardRemoveFinished events update
            // the pile counters correctly. The rack supplies a short, explicit
            // destination animation because autoplay pile visuals were skipped.
            await MgrPerformanceVisuals.PlayExitAnimation(
                player,
                entry,
                entry.Card.Pile?.Type);

            state.Remove(entry);
            entry.ResetRemainingTurns();
            MgrPerformanceVisuals.Show(player, state.Entries);
        }
    }
}
