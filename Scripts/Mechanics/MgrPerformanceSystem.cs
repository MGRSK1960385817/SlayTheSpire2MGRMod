using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Powers;
using SlayTheSpire2MGRMod.Relics;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Centralized scheduler for the MGR Performance mechanic. A stable snapshot is
/// processed each turn so effects that enqueue new cards cannot disturb the
/// current pass or reorder older entries.
/// </summary>
public static class MgrPerformanceSystem
{
    private const int DefaultExternalPerformanceTurns = 1;
    private static readonly HashSet<CardModel> CompletingCards = [];
    private static readonly HashSet<MgrPerformanceEntry> ResolvingEntries = [];
    private static readonly Dictionary<MgrPerformanceEntry, float> ActiveVfxWaitScales = [];
    private readonly record struct PendingEntryReplacement(
        CardModel Card,
        int PerformanceTurns);

    private static readonly Dictionary<MgrPerformanceEntry, PendingEntryReplacement>
        PendingEntryReplacements = [];
    private static readonly Dictionary<CardModel, PendingEntryReplacement>
        PendingPlayedCardReplacements = [];
    private static readonly Dictionary<CardModel, int> PendingEnqueueBonuses = [];
    private static readonly Dictionary<Player, int> ActivePassDepths = [];

    public static void ClearAll()
    {
        CompletingCards.Clear();
        ResolvingEntries.Clear();
        ActiveVfxWaitScales.Clear();
        PendingEntryReplacements.Clear();
        PendingPlayedCardReplacements.Clear();
        PendingEnqueueBonuses.Clear();
        ActivePassDepths.Clear();
        MgrPerformanceVisuals.ClearAll();
        MgrPerformanceStateStore.Clear();
        MgrPerformanceModifierState.Clear();
    }

    public static int GetInitialPerformanceTurns(CardModel card)
    {
        int printed = card is MgrCard mgrCard ? mgrCard.InitialPerformanceTurns : 0;
        return Math.Max(0, printed + MgrPerformanceModifierState.GetAdditionalPerformances(card));
    }

    internal static void RefreshQueueDependentCardCosts(Player player)
    {
        if (NPlayerHand.Instance is not { } hand)
            return;

        foreach (CardModel card in PileType.Hand.GetPile(player).Cards)
        {
            hand.GetCard(card)?.UpdateVisuals(
                PileType.Hand,
                CardPreviewMode.Normal);
        }
    }

    public static bool IsPerformanceCard(CardModel card) =>
        GetInitialPerformanceTurns(card) > 0;

    public static bool ShouldHoldForResolvedPlay(
        CardModel card,
        ResourceInfo resources)
    {
        if (IsCompletingPerformance(card))
            return false;

        // Externally inserted cards (and cards exchanged into the rack) may not
        // print Performance at all. Their live queue entry is authoritative:
        // keep them in Play until the scheduler marks their final trigger.
        if (MgrPerformanceStateStore.TryGet(card.Owner, out MgrPerformanceState state) &&
            state.Contains(card))
        {
            return true;
        }

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

    /// <summary>
    /// Shortens only an explicitly requested cinematic wait while this exact
    /// card is resolving from the Performance rack. CardCmd.AutoPlay and every
    /// gameplay command remain awaited, so damage, notes, powers and victory
    /// checks cannot overtake one another.
    /// </summary>
    public static float GetVisualWaitDuration(CardModel card, float normalSeconds)
    {
        foreach (MgrPerformanceEntry entry in ResolvingEntries)
        {
            if (!ReferenceEquals(entry.Card, card) ||
                !ActiveVfxWaitScales.TryGetValue(entry, out float sequenceScale))
            {
                continue;
            }

            return MathF.Max(
                (float)MgrVisualTuning.Performances.MinimumPerformanceVfxWaitSeconds,
                normalSeconds *
                MgrVisualTuning.Performances.PerformanceVfxWaitMultiplier *
                sequenceScale);
        }

        return normalSeconds;
    }

    public static bool IsResolvingPerformance(CardModel card) =>
        ResolvingEntries.Any(entry => ReferenceEquals(entry.Card, card));

    /// <summary>
    /// Requests an in-place queue replacement after the currently resolving
    /// card has finished its play hooks. Externally exchanged cards use their
    /// own printed Performance value, or one turn if they print none.
    /// </summary>
    public static bool QueueResolvingCardReplacement(
        CardModel outgoingCard,
        CardModel incomingCard)
    {
        if (ReferenceEquals(outgoingCard, incomingCard) ||
            !ReferenceEquals(outgoingCard.Owner, incomingCard.Owner))
        {
            return false;
        }

        MgrPerformanceEntry? entry = ResolvingEntries.FirstOrDefault(
            candidate => ReferenceEquals(candidate.Card, outgoingCard));
        if (entry is null || PendingEntryReplacements.ContainsKey(entry))
            return false;

        PendingEntryReplacements[entry] = new PendingEntryReplacement(
            incomingCard,
            GetExternalPerformanceTurns(incomingCard));
        return true;
    }

    /// <summary>
    /// Replaces a newly played Performance card with another physical card once
    /// AfterCardPlayed creates the queue entry. Used by Puppet Clown because its
    /// pile exchange occurs inside OnPlay, before the normal entry is registered.
    /// </summary>
    public static bool QueuePlayedCardReplacement(
        CardModel outgoingCard,
        CardModel incomingCard)
    {
        if (ReferenceEquals(outgoingCard, incomingCard) ||
            !ReferenceEquals(outgoingCard.Owner, incomingCard.Owner) ||
            PendingPlayedCardReplacements.ContainsKey(outgoingCard))
        {
            return false;
        }

        PendingPlayedCardReplacements[outgoingCard] =
            new PendingEntryReplacement(
                incomingCard,
                GetExternalPerformanceTurns(incomingCard));
        return true;
    }

    private static int GetExternalPerformanceTurns(CardModel card) =>
        Math.Max(DefaultExternalPerformanceTurns, GetInitialPerformanceTurns(card));

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

        int initialTurns = Math.Max(
            DefaultExternalPerformanceTurns,
            GetInitialPerformanceTurns(card));
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
        CardModel card) => EnqueueCardFromPile(
            player,
            card,
            Math.Max(
                DefaultExternalPerformanceTurns,
                GetInitialPerformanceTurns(card)),
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
        if (ShouldStopPerformanceSequence(player) ||
            !MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return;
        }

        BeginPerformancePass(player);
        try
        {
            int animationIndex = 0;
            foreach (MgrPerformanceEntry entry in state.Entries.ToArray())
            {
                if (ShouldStopPerformanceSequence(player))
                    break;

                if (!state.Entries.Contains(entry) || ResolvingEntries.Contains(entry))
                    continue;

                float durationScale = GetSequentialAnimationDurationScale(animationIndex);
                await MgrPerformanceVisuals.PlayTriggerAnimation(
                    player,
                    entry,
                    consumesRemaining: false,
                    durationScale: durationScale);
                ResolvingEntries.Add(entry);
                ActiveVfxWaitScales[entry] = durationScale;
                try
                {
                    await AutoPlayPerformanceCard(choiceContext, entry.Card);
                }
                finally
                {
                    ActiveVfxWaitScales.Remove(entry);
                    ResolvingEntries.Remove(entry);
                    await MgrPerformanceVisuals.PlayTriggerCompletionAnimation(
                        player,
                        entry,
                        durationScale);
                }

                if (TryApplyPendingReplacement(player, state, entry))
                {
                    if (ShouldStopPerformanceSequence(player))
                        break;

                    animationIndex++;
                    continue;
                }

                // AutoPlay returns only after the card and its queued effects have
                // resolved. Re-check the engine's combat-ending flag here so a
                // lethal performance immediately hands control back to Tower 2's
                // normal victory flow instead of starting the next rack card.
                if (ShouldStopPerformanceSequence(player))
                    break;

                animationIndex++;
            }
        }
        finally
        {
            EndPerformancePass(player);
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
    /// Immediately resolves one ordinary Performance step for only the
    /// rightmost queued card. Entries are stored rightmost-first, matching the
    /// rack layout and the existing rightmost-card modifier helpers.
    /// </summary>
    public static Task TriggerRightmostQueuedCardOnceAndConsume(
        PlayerChoiceContext choiceContext,
        Player player) => ConsumeOnePass(
            choiceContext,
            player,
            rightmostOnly: true);

    /// <summary>
    /// Immediately finishes every currently queued Performance card without
    /// playing its remaining steps. Cards use their normal completion
    /// destination and still receive their Performance-finished hook.
    /// </summary>
    public static Task<int> EndAllPerformances(
        PlayerChoiceContext choiceContext,
        Player player) => EndAllPerformancesCore(
            choiceContext,
            player,
            finisherSource: null,
            onEachEnded: null);

    /// <summary>
    /// Maguro Dash variant of EndAllPerformances. The source card is represented
    /// by a presentation-only silhouette that crosses the rack; it never becomes
    /// a queue entry. The callback preserves the card's one-extra-hit-per-ended-
    /// entry rule while visually pairing each hit with the entry it consumed.
    /// </summary>
    public static Task<int> EndAllPerformancesWithFinisher(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel finisherSource,
        Func<int, Task> onEachEnded) => EndAllPerformancesCore(
            choiceContext,
            player,
            finisherSource,
            onEachEnded);

    private static async Task<int> EndAllPerformancesCore(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel? finisherSource,
        Func<int, Task>? onEachEnded)
    {
        if (ShouldStopPerformanceSequence(player) ||
            !MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return 0;
        }

        int ended = 0;
        bool hasFinisher = finisherSource is not null;
        if (hasFinisher)
            BeginPerformancePass(player);

        try
        {
            if (hasFinisher)
            {
                await MgrPerformanceVisuals.BeginFinisher(
                    player,
                    finisherSource!,
                    state.Entries);
            }

            foreach (MgrPerformanceEntry entry in state.Entries.ToArray())
            {
                // A finisher can itself be auto-played from the rack. Moving
                // that physical card while its OnPlay is still running would
                // leave the outer scheduler holding a stale resolving entry.
                // Its current trigger is consumed normally by the outer pass;
                // every other entry is still ended immediately.
                if (!state.Entries.Contains(entry) || ResolvingEntries.Contains(entry))
                    continue;

                if (hasFinisher)
                {
                    await MgrPerformanceVisuals.PlayFinisherStrike(
                        player,
                        entry,
                        ended);
                }

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

                if (player.Creature.GetPower<ChaosMagicPower>() is { } chaosMagic)
                    await chaosMagic.OnPerformanceEnded(player);

                if (player.GetRelic<BlackGoldRecord>() is { } blackGoldRecord)
                    await blackGoldRecord.OnPerformanceEnded(player);

                await MgrPerformanceVisuals.PlayExitAnimation(
                    player,
                    entry,
                    entry.Card.Pile?.Type,
                    hasFinisher
                        ? MgrVisualTuning.Performances.FinisherEndedCardExitDurationScale
                        : 1f);
                NotifySkippedPileAnimationFinished(entry.Card);

                state.Remove(entry);
                entry.ResetRemainingTurns();
                ended++;
                // Keep the original rack geometry during the finisher so the
                // silhouette visibly travels from slot to slot. Each ended
                // view has already flown away; the surviving views are only
                // reflowed after the whole sequence finishes.
                if (!hasFinisher)
                    MgrPerformanceVisuals.Show(player, state.Entries);

                if (onEachEnded is not null && !ShouldStopPerformanceSequence(player))
                    await onEachEnded(ended);

                if (ShouldStopPerformanceSequence(player))
                    break;
            }
        }
        finally
        {
            if (hasFinisher)
            {
                try
                {
                    await MgrPerformanceVisuals.CompleteFinisher(
                        player,
                        animate: !ShouldStopPerformanceSequence(player));
                }
                finally
                {
                    if (!ShouldStopPerformanceSequence(player))
                        MgrPerformanceVisuals.Show(player, state.Entries);
                    EndPerformancePass(player);
                }
            }
        }

        return ended;
    }

    public static void ObserveResolvedCardPlay(CardPlay cardPlay)
    {
        if (!cardPlay.IsLastInSeries)
            return;

        CardModel card = cardPlay.Card;
        MgrPerformanceState state = MgrPerformanceStateStore.For(card.Owner);
        bool replacedAfterPlay = PendingPlayedCardReplacements.Remove(
            card,
            out PendingEntryReplacement pendingReplacement);
        CardModel queuedCard = replacedAfterPlay
            ? pendingReplacement.Card
            : card;
        int initialPerformanceTurns = replacedAfterPlay
            ? pendingReplacement.PerformanceTurns
            : GetInitialPerformanceTurns(card);
        if (replacedAfterPlay)
            PendingEnqueueBonuses.Remove(card);

        if (state.Contains(queuedCard) || initialPerformanceTurns <= 0)
            return;

        int bonusPerformances = 0;
        if (!replacedAfterPlay &&
            !cardPlay.IsAutoPlay &&
            card.Owner.GetRelic<MiniStage>() is { } miniStage &&
            miniStage.TryGrantPerformanceBonus())
        {
            bonusPerformances = 1;
        }


        if (!replacedAfterPlay &&
            PendingEnqueueBonuses.Remove(card, out int pendingBonus))
            bonusPerformances = checked(bonusPerformances + pendingBonus);

        MgrPerformanceEntry? entry = state.Enqueue(
            queuedCard,
            initialPerformanceTurns,
            bonusPerformances);
        if (entry is null)
            return;

        state.RecordPerformanceCardPlayed();
        int queuedBeforeThisTurn = state.RecordPlayedEntryQueuedThisTurn();

        // The normal play that entered the sequence does not consume a turn.
        // Both the rack replica and its entry animation are deferred until the
        // native play pipeline emits Played. Creating a second NCard earlier can
        // confuse Tower 2's model-based NCard.FindOnTable result routing.
        MgrPerformanceVisuals.QueueEntryAnimationAfterPlay(
            card.Owner,
            state.Entries,
            entry,
            queuedBeforeThisTurn,
            playedCard: replacedAfterPlay ? card : null,
            animateEntry: !replacedAfterPlay);
    }

    public static async Task PerformAtTurnStart(
        PlayerChoiceContext choiceContext,
        Player player) => await ConsumeOnePass(choiceContext, player);

    private static async Task ConsumeOnePass(
        PlayerChoiceContext choiceContext,
        Player player,
        bool rightmostOnly = false)
    {
        if (ShouldStopPerformanceSequence(player) ||
            !MgrPerformanceStateStore.TryGet(player, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return;
        }

        MgrPerformanceEntry[] turnOrder = rightmostOnly
            ? state.Entries.Take(1).ToArray()
            : state.Entries.ToArray();
        BeginPerformancePass(player);
        try
        {
            int animationIndex = 0;
            foreach (MgrPerformanceEntry entry in turnOrder)
            {
                if (ShouldStopPerformanceSequence(player))
                    break;

                // A queued card may itself request an immediate Performance pass.
                // Keep that nested pass useful for every other queued card, but do
                // not let it re-enter this card before its current play has returned.
                if (!state.Entries.Contains(entry) || ResolvingEntries.Contains(entry))
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

                ResolvingEntries.Add(entry);
                ActiveVfxWaitScales[entry] = durationScale;
                try
                {
                    await AutoPlayPerformanceCard(choiceContext, entry.Card);
                }
                finally
                {
                    ActiveVfxWaitScales.Remove(entry);
                    ResolvingEntries.Remove(entry);
                    CompletingCards.Remove(entry.Card);
                    await MgrPerformanceVisuals.PlayTriggerCompletionAnimation(
                        player,
                        entry,
                        durationScale);
                }


                // A card such as Puppet Clown can exchange its physical place
                // with another card while resolving. The outgoing card has
                // already paid for this trigger, so preserve the incoming card's
                // newly initialized counter for the next pass instead of
                // decrementing it.
                if (TryApplyPendingReplacement(player, state, entry))
                {
                    if (ShouldStopPerformanceSequence(player))
                        break;

                    animationIndex++;
                    continue;
                }

                entry.ConsumeOnePerformance();
                bool combatEnded = ShouldStopPerformanceSequence(player);

                if (entry.RemainingPerformanceTurns > 0)
                {
                    MgrPerformanceVisuals.Show(player, state.Entries);

                    // This card really did perform once, so its counter is kept.
                    // Later cards in the snapshot have not acted and retain their
                    // counters untouched while the combat transitions to results.
                    if (combatEnded)
                        break;

                    animationIndex++;
                    continue;
                }

                // Do not start any Performance-finished gameplay after victory.
                // The just-played card has already completed its ordinary effects;
                // only its bookkeeping must now be detached from the rack.
                if (!combatEnded && entry.Card is MgrCard mgrCard)
                {
                    await mgrCard.OnPerformanceFinished(
                        choiceContext,
                        new PerformanceCompletionContext(
                            player,
                            entry.InitialPerformanceTurns,
                            willExhaust));
                }

                if (!combatEnded &&
                    player.Creature.GetPower<ChaosMagicPower>() is { } chaosMagic)
                {
                    await chaosMagic.OnPerformanceEnded(player);
                }

                if (!combatEnded && player.GetRelic<BlackGoldRecord>() is { } blackGoldRecord)
                    await blackGoldRecord.OnPerformanceEnded(player);

                // The final autoplay has already used the engine's normal result
                // routing. Because its native pile VFX was skipped, the rack supplies
                // both the destination animation and the CardAddFinished notification
                // that NCardFlyVfx would ordinarily emit when it reaches the pile.
                if (!combatEnded)
                {
                    await MgrPerformanceVisuals.PlayExitAnimation(
                        player,
                        entry,
                        entry.Card.Pile?.Type,
                        durationScale);
                }

                NotifySkippedPileAnimationFinished(entry.Card);

                state.Remove(entry);
                entry.ResetRemainingTurns();
                MgrPerformanceVisuals.Show(player, state.Entries);

                if (combatEnded)
                    break;

                animationIndex++;
            }
        }
        finally
        {
            EndPerformancePass(player);
        }
    }

    /// <summary>
    /// Nested Performance passes share one visual active period. Without the
    /// depth counter, an inner Adios pass would switch the staff back to idle
    /// while its outer pass was still resolving.
    /// </summary>
    private static void BeginPerformancePass(Player player)
    {
        int depth = ActivePassDepths.GetValueOrDefault(player);
        ActivePassDepths[player] = checked(depth + 1);
        if (depth == 0)
            MgrPerformanceVisuals.SetPerforming(player, true);
    }

    private static void EndPerformancePass(Player player)
    {
        if (!ActivePassDepths.TryGetValue(player, out int depth) || depth <= 1)
        {
            ActivePassDepths.Remove(player);
            MgrPerformanceVisuals.SetPerforming(player, false);
            return;
        }

        ActivePassDepths[player] = depth - 1;
    }

    private static float GetSequentialAnimationDurationScale(int animationIndex) =>
        MathF.Max(
            MgrVisualTuning.Performances.MinimumSequentialTriggerDurationScale,
            MathF.Pow(
                MgrVisualTuning.Performances
                    .SequentialTriggerDurationMultiplierPerCard,
                Math.Max(0, animationIndex)));

    private static async Task AutoPlayPerformanceCard(
        PlayerChoiceContext choiceContext,
        CardModel card)
    {
        bool bypassLocalUnplayable =
            card.Type is CardType.Curse or CardType.Status &&
            card.GetKeywordsWithSources(KeywordSources.Local)
                .Contains(CardKeyword.Unplayable);

        if (bypassLocalUnplayable)
            card.RemoveKeyword(CardKeyword.Unplayable);

        try
        {
            await CardCmd.AutoPlay(
                choiceContext,
                card,
                target: null,
                skipCardPileVisuals: true);
        }
        finally
        {
            if (bypassLocalUnplayable &&
                !card.GetKeywordsWithSources(KeywordSources.Local)
                    .Contains(CardKeyword.Unplayable))
            {
                card.AddKeyword(CardKeyword.Unplayable);
            }
        }
    }

    private static bool TryApplyPendingReplacement(
        Player player,
        MgrPerformanceState state,
        MgrPerformanceEntry outgoingEntry)
    {
        if (!PendingEntryReplacements.Remove(
                outgoingEntry,
                out PendingEntryReplacement pendingReplacement))
        {
            return false;
        }

        MgrPerformanceEntry? replacement = state.Replace(
            outgoingEntry,
            pendingReplacement.Card,
            pendingReplacement.PerformanceTurns);
        if (replacement is null)
            return false;

        MgrPerformanceVisuals.Show(player, state.Entries);
        return true;
    }

    private static bool ShouldStopPerformanceSequence(Player player) =>
        CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead;

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
