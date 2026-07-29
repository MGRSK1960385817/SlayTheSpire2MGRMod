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
    private static readonly Dictionary<CardModel, int> PendingEnqueueBonuses = [];

    public static void ClearAll()
    {
        CompletingCards.Clear();
        PendingEnqueueBonuses.Clear();
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

    public static bool ShouldHoldForResolvedPlay(
        CardModel card,
        ResourceInfo resources)
    {
        if (IsCompletingPerformance(card))
            return false;

        int turns = card is MgrCard mgrCard
            ? mgrCard.GetPerformanceTurnsForResultRouting(resources)
            : GetInitialPerformanceTurns(card);
        return turns > 0;
    }

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

    public static int AddPerformancesToRightmostQueuedCards(
        Player player,
        int cardCount,
        int amount)
    {
        if (cardCount <= 0 || amount <= 0 ||
            !MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state))
        {
            return 0;
        }

        MgrPerformanceEntry[] targets = state.Entries.Take(cardCount).ToArray();
        foreach (MgrPerformanceEntry entry in targets)
        {
            MgrPerformanceModifierState.Grant(entry.Card, amount);
            entry.AddPerformanceTurns(amount);
        }

        if (targets.Length > 0)
            MgrPerformanceVisuals.Show(player, state.Entries);
        return targets.Length;
    }

    /// <summary>
    /// Adds a one-shot bonus to the next manual play that enters Performance.
    /// Unlike combat card mutation, this changes only that queue entry and is
    /// consumed immediately after the resolved play.
    /// </summary>
    public static void AddPendingEnqueueBonus(CardModel card, int amount)
    {
        if (amount <= 0)
            return;

        PendingEnqueueBonuses[card] = checked(
            (PendingEnqueueBonuses.TryGetValue(card, out int current) ? current : 0) +
            amount);
    }

    /// <summary>
    /// Registers a newly generated combat card directly in the Performance
    /// sequence without resolving an ordinary card play first. Cards with a
    /// printed Performance value keep it; all other cards perform once.
    /// </summary>
    public static async Task<MgrPerformanceEntry> EnqueueGeneratedCard(
        Player player,
        CardModel card)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(card);
        if (card.Pile is not null)
        {
            throw new InvalidOperationException(
                $"Generated Performance card {card.Id} already belongs to {card.Pile.Type}.");
        }

        await CardPileCmd.AddGeneratedCardToCombat(
            card,
            PileType.Play,
            player);

        int initialTurns = Math.Max(1, GetInitialPerformanceTurns(card));
        MgrPerformanceState state = MgrPerformanceStateStore.For(player);
        MgrPerformanceEntry? entry = state.Enqueue(card, initialTurns);
        if (entry is null)
        {
            throw new InvalidOperationException(
                $"Could not enqueue generated Performance card {card.Id}.");
        }

        MgrPerformanceVisuals.Show(player, state.Entries);
        return entry;
    }

    /// <summary>
    /// Moves an existing hand card through Tower 2's normal pile command and
    /// then holds it in the Performance rack. This is used by delayed hand
    /// effects such as Coward Rocket and deliberately does not count as a play.
    /// </summary>
    public static Task<MgrPerformanceEntry?> EnqueueCardFromHand(
        Player player,
        CardModel card,
        int initialTurns) => EnqueueCardFromPile(
            player,
            card,
            initialTurns,
            PileType.Hand);

    private static async Task<MgrPerformanceEntry?> EnqueueCardFromPile(
        Player player,
        CardModel card,
        int initialTurns,
        PileType expectedSourcePile)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(card);
        if (card.Pile?.Type != expectedSourcePile || initialTurns <= 0)
            return null;

        await CardPileCmd.Add(card, PileType.Play);
        MgrPerformanceState state = MgrPerformanceStateStore.For(player);
        MgrPerformanceEntry? entry = state.Enqueue(card, initialTurns);
        if (entry is null)
            return null;

        MgrPerformanceVisuals.Show(player, state.Entries);
        MgrPerformanceVisuals.QueueEntryAnimation(player, entry);
        return entry;
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

        MgrPerformanceVisuals.SetPerforming(player, true);
        try
        {
            int animationIndex = 0;
            foreach (MgrPerformanceEntry entry in state.Entries.ToArray())
            {
                if (!state.Entries.Contains(entry))
                    continue;

                float durationScale = GetSequentialAnimationDurationScale(animationIndex);
                await MgrPerformanceVisuals.PlayTriggerAnimation(
                    player,
                    entry,
                    consumesRemaining: false,
                    durationScale: durationScale);
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
                    await MgrPerformanceVisuals.PlayTriggerCompletionAnimation(
                        player,
                        entry,
                        durationScale);
                }

                animationIndex++;
            }
        }
        finally
        {
            MgrPerformanceVisuals.SetPerforming(player, false);
        }

        MgrPerformanceVisuals.Show(player, state.Entries);
    }

    /// <summary>
    /// Immediately resolves one ordinary Performance step for every queued
    /// card. Unlike TriggerQueuedCardsOnce, this consumes remaining turns and
    /// completes/routs cards whose counter reaches zero.
    /// </summary>
    public static Task TriggerQueuedCardsOnceAndConsume(
        PlayerChoiceContext choiceContext,
        Player player) => ConsumeOnePass(choiceContext, player);

    /// <summary>
    /// Immediately finishes every currently queued Performance card without
    /// playing its remaining steps. Cards use their normal completion
    /// destination and still receive their Performance-finished hook.
    /// </summary>
    public static async Task<int> EndAllPerformances(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return 0;
        }

        int ended = 0;
        foreach (MgrPerformanceEntry entry in state.Entries.ToArray())
        {
            if (!state.Entries.Contains(entry))
                continue;

            bool isOrdinaryPower = entry.Card.Type == CardType.Power && !entry.Card.IsDupe;
            bool willExhaust = !isOrdinaryPower &&
                (entry.Card.Keywords.Contains(CardKeyword.Exhaust) ||
                 entry.Card.ExhaustOnNextPlay);

            if (isOrdinaryPower)
            {
                entry.Card.RemoveFromState();
            }
            else if (willExhaust)
            {
                await CardCmd.Exhaust(
                    choiceContext,
                    entry.Card,
                    skipVisuals: true);
            }
            else
            {
                await CardPileCmd.Add(
                    entry.Card,
                    PileType.Discard,
                    skipVisuals: true);
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

            await MgrPerformanceVisuals.PlayExitAnimation(
                player,
                entry,
                entry.Card.Pile?.Type);
            NotifySkippedPileAnimationFinished(entry.Card);

            state.Remove(entry);
            entry.ResetRemainingTurns();
            ended++;
            MgrPerformanceVisuals.Show(player, state.Entries);
        }

        return ended;
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


        if (PendingEnqueueBonuses.Remove(card, out int pendingBonus))
            bonusPerformances = checked(bonusPerformances + pendingBonus);

        MgrPerformanceEntry? entry = state.Enqueue(
            card,
            initialPerformanceTurns,
            bonusPerformances);
        if (entry is null)
            return;

        // The normal play that entered the sequence does not consume a turn.
        // Both the rack replica and its entry animation are deferred until the
        // native play pipeline emits Played. Creating a second NCard earlier can
        // confuse Tower 2's model-based NCard.FindOnTable result routing.
        MgrPerformanceVisuals.QueueEntryAnimationAfterPlay(
            card.Owner,
            state.Entries,
            entry);
    }

    public static async Task PerformAtTurnStart(
        PlayerChoiceContext choiceContext,
        Player player) => await ConsumeOnePass(choiceContext, player);

    private static async Task ConsumeOnePass(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return;
        }

        MgrPerformanceEntry[] turnOrder = state.Entries.ToArray();
        MgrPerformanceVisuals.SetPerforming(player, true);
        try
        {
            int animationIndex = 0;
            foreach (MgrPerformanceEntry entry in turnOrder)
            {
                if (!state.Entries.Contains(entry))
                    continue;

                float durationScale = GetSequentialAnimationDurationScale(animationIndex);
                bool isOrdinaryPower = entry.Card.Type == CardType.Power && !entry.Card.IsDupe;
                bool willExhaust = !isOrdinaryPower &&
                    (entry.Card.Keywords.Contains(CardKeyword.Exhaust) ||
                     entry.Card.ExhaustOnNextPlay);
                bool isFinalPerformance = entry.RemainingPerformanceTurns <= 1;

                await MgrPerformanceVisuals.PlayTriggerAnimation(
                    player,
                    entry,
                    consumesRemaining: true,
                    durationScale: durationScale);

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
                    await MgrPerformanceVisuals.PlayTriggerCompletionAnimation(
                        player,
                        entry,
                        durationScale);
                }

                entry.ConsumeOnePerformance();

                if (entry.RemainingPerformanceTurns > 0)
                {
                    animationIndex++;
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
                // routing. Because its native pile VFX was skipped, the rack supplies
                // both the destination animation and the CardAddFinished notification
                // that NCardFlyVfx would ordinarily emit when it reaches the pile.
                await MgrPerformanceVisuals.PlayExitAnimation(
                    player,
                    entry,
                    entry.Card.Pile?.Type,
                    durationScale);

                NotifySkippedPileAnimationFinished(entry.Card);

                state.Remove(entry);
                entry.ResetRemainingTurns();
                MgrPerformanceVisuals.Show(player, state.Entries);
                animationIndex++;
            }
        }
        finally
        {
            MgrPerformanceVisuals.SetPerforming(player, false);
        }
    }

    private static float GetSequentialAnimationDurationScale(int animationIndex) =>
        MathF.Max(
            MgrVisualTuning.Performances.MinimumSequentialTriggerDurationScale,
            1f - Math.Max(0, animationIndex) *
            MgrVisualTuning.Performances.SequentialTriggerAccelerationPerCard);

    /// <summary>
    /// CardPileCmd normally lets NCardFlyVfx emit this notification after the
    /// physical card reaches Discard/Exhaust. Performance uses its own rack
    /// animation, so this is the matching completion point for skipped VFX.
    /// </summary>
    private static void NotifySkippedPileAnimationFinished(CardModel card)
    {
        if (card.Pile is { Type: PileType.Discard or PileType.Exhaust } resultPile)
            resultPile.InvokeCardAddFinished();
    }
}
