using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Cards;
using MGRMod.Powers;
using MGRMod.Relics;
using MGRMod.Telemetry;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace MGRMod.Mechanics;

/// <summary>
/// Global combat listener that generates exactly one note for every resolved CardPlay
/// owned by an MGR player. Auto-play and every Replay resolution are intentionally included.
/// </summary>
[RegisterSingleton]
public sealed class MgrNoteSystem : HookedSingletonModel
{
    // Explicit integer weights make random-note tuning work like MGR's random
    // curse pool. Removing one entry automatically redistributes its chance
    // among every remaining kind when the total weight is recalculated.
    private static readonly (NoteKind Kind, int Weight)[] RandomBasicNoteWeights =
    [
        (NoteKind.Attack, 38),
        (NoteKind.Skill, 38),
        (NoteKind.Status, 5),
        (NoteKind.Power, 15),
        (NoteKind.Curse, 4)
    ];

    private readonly Dictionary<Player, CardModel> _lastPlayedCards = [];

    public MgrNoteSystem() : base(HookType.Combat)
    {
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay);

        CardModel card = cardPlay.Card;
        if (card is MgrCard &&
            card.Rarity is CardRarity.Rare or CardRarity.Ancient)
        {
            MgrAbilityVfx.PlayGoldCardCast(card);
        }
        else if (card is MgrCard && card.Rarity == CardRarity.Uncommon)
        {
            MgrAbilityVfx.PlayFeaturedUncommonCardCast(card);
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        MgrPerformanceSystem.ClearAll();
        MgrCombatCardMutationState.Clear();
        MgrNoteVisuals.ClearAll();
        MgrCombatStateStore.Clear();
        _lastPlayedCards.Clear();

        if (CurrentCombatState is not { } combatState)
            return;

        // Every MGR player gets a visible four-slot rack before any notes are
        // generated. Mini Microphone then fills the first three slots, leaving the
        // fourth visibly empty until another note completes the chord.
        var choiceContext = new ThrowingPlayerChoiceContext();
        foreach (var player in combatState.Players)
        {
            if (player.Character is not MgrCharacter)
                continue;

            if (player.GetRelic<MgrFumo>() is { IsUsedUp: false } fumo)
            {
                fumo.Flash();
                await PowerCmd.Apply<FortePower>(
                    choiceContext,
                    player.Creature,
                    1m,
                    player.Creature,
                    cardSource: null);
            }

            MgrCombatState state = MgrCombatStateStore.For(player);
            state.SetForteSnapshot(player.Creature.GetPowerAmount<FortePower>());
            MgrNoteVisuals.Show(
                player,
                state.Phrase.Notes,
                state.Phrase.Capacity,
                state.Forte,
                clearAfterDelay: false);

            // Create the empty Performance rack together with the note rack so
            // its staff is already present before the first Performance card.
            MgrPerformanceState performanceState =
                MgrPerformanceStateStore.For(player);
            MgrPerformanceVisuals.Show(player, performanceState.Entries);

            if (player.GetRelic<BookOfGrudges>() is { } plectrum)
            {
                plectrum.Flash();
                for (int index = 0; index < plectrum.CombatStartAttackNotes; index++)
                    await ChannelNote(choiceContext, player, NoteKind.Attack);
            }

            if (player.GetRelic<MyFriend>() is not { IsUsedUp: false } relic)
                continue;

            relic.Flash();
            await ChannelNote(choiceContext, player, NoteKind.Attack);
            await ChannelNote(choiceContext, player, NoteKind.Skill);
            await ChannelNote(choiceContext, player, NoteKind.Power);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay);

        var player = cardPlay.Card.Owner;
        if (player.Character is not MgrCharacter)
            return;

        NoteKind playedKind = CardNoteResolver.Resolve(cardPlay.Card);
        await ChannelNote(choiceContext, player, playedKind);

        MgrPerformanceSystem.ObserveResolvedCardPlay(cardPlay);

        // Treat Replay resolutions as one logical card play. The first-card
        // growth happens after that card resolves; the final card cannot be
        // known until combat ends and is handled in AfterCombatEnd.
        if (cardPlay.IsLastInSeries)
        {
            bool isFirstCardPlayed = !_lastPlayedCards.ContainsKey(player);
            if (isFirstCardPlayed && cardPlay.Card is EastOfTimeline firstTimeline)
                firstTimeline.IncreaseNotesPermanently();

            _lastPlayedCards[player] = cardPlay.Card;
        }
    }

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        // Performance entries own their physical card only while it remains in
        // Play. Native effects such as Bolas returning itself to Hand keep their
        // normal movement and presentation; the rack releases the same model
        // after that move instead of auto-playing it again from its new pile.
        MgrPerformanceSystem.ReconcileQueuedCardPile(card);
        return Task.CompletedTask;
    }

#if STS2_V107
    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (card.Owner.Character is MgrCharacter &&
            MgrPerformanceSystem.ShouldHoldForResolvedPlay(card, resources))
        {
            return (PileType.Play, CardPilePosition.Bottom);
        }

        return (pileType, position);
    }
#else
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card.Owner.Character is MgrCharacter &&
            MgrPerformanceSystem.ShouldHoldForResolvedPlay(card, resources))
        {
            return new CardLocation(card.Owner, PileType.Play, CardPilePosition.Bottom);
        }

        return location;
    }
#endif

    /// <summary>
    /// Unified entry point for card plays, discard-based generation and future card effects.
    /// Filling the current capacity resolves a chord and triggers its notes from left to right.
    /// </summary>
    public static async Task<bool> ChannelNote(
        PlayerChoiceContext choiceContext,
        Player player,
        NoteKind kind)
    {
        if (ShouldStopNoteSequence(player))
            return false;

        if (kind == NoteKind.Attack &&
            player.Creature.GetPowerAmount<AttackNoteSilencePower>() > 0m)
        {
            return false;
        }

        int copies = player.Creature.GetPowerAmount<DoubleNotesPower>() > 0m ? 2 : 1;
        bool generatedAny = false;
        for (int copy = 0; copy < copies; copy++)
        {
            if (ShouldStopNoteSequence(player))
                break;

            await ChannelSingleNote(choiceContext, player, kind);
            generatedAny = true;
        }
        return generatedAny;
    }

    /// <summary>
    /// STS1 "Improvise": generates one weighted random basic note.
    /// </summary>
    public static Task ChannelRandomBasicNote(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        // Do not advance combat-generation RNG for notes that are discarded
        // because the encounter has already ended.
        if (ShouldStopNoteSequence(player))
            return Task.CompletedTask;

        bool suppressAttack =
            player.Creature.GetPowerAmount<AttackNoteSilencePower>() > 0m;
        int totalWeight = RandomBasicNoteWeights
            .Where(entry => !suppressAttack || entry.Kind != NoteKind.Attack)
            .Sum(static entry => entry.Weight);
        if (totalWeight <= 0)
            throw new InvalidOperationException("The random Basic Note pool has no positive weights.");

        int roll = player.RunState.Rng.CombatCardGeneration.NextInt(0, totalWeight);
        foreach ((NoteKind kind, int weight) in RandomBasicNoteWeights)
        {
            if (suppressAttack && kind == NoteKind.Attack)
                continue;

            roll -= weight;
            if (roll < 0)
                return ChannelNote(choiceContext, player, kind);
        }

        // Defensive fallback for an RNG implementation with unexpected bounds.
        NoteKind fallback = RandomBasicNoteWeights
            .Last(entry => !suppressAttack || entry.Kind != NoteKind.Attack)
            .Kind;
        return ChannelNote(choiceContext, player, fallback);
    }

    private static async Task ChannelSingleNote(
        PlayerChoiceContext choiceContext,
        Player player,
        NoteKind kind)
    {
        MgrNote note = MgrNoteFactory.Create(kind);
        MgrCombatState state = MgrCombatStateStore.For(player);
        state.SetForteSnapshot(player.Creature.GetPowerAmount<FortePower>());
        int enteringIndex = state.Phrase.Notes.Count;
        int notesGeneratedBefore = state.NotesGeneratedThisTurn;
        int chordTriggersBefore = state.ChordTriggersThisTurn;
        PhraseResolution? resolution = state.AddNote(note);
        MgrRunTelemetryAccumulator.RecordNoteGenerated(player, kind);
        if (kind == NoteKind.Starry)
            MgrStarryNoteVfx.Spawn(player);
        RefreshConditionalCardGlows(player);

        MgrAudio.PlayNoteChannel();

        // Like the Defect's OrbQueue/NOrbManager split, state owns the notes and
        // this adapter mirrors them into persistent Godot nodes. Awaiting the
        // entrance tween gives multi-note effects a clear left-to-right rhythm.
        await MgrNoteVisuals.ShowChanneledNote(
            player,
            resolution?.Notes ?? state.Phrase.Notes,
            state.Phrase.Capacity,
            state.Forte,
            enteringIndex,
            notesGeneratedBefore,
            chordTriggersBefore,
            clearAfterDelay: resolution is not null);

        if (resolution is null || ShouldStopNoteSequence(player))
            return;

        MgrAudio.PlayChord();
        await TriggerResolvedChord(
            choiceContext,
            player,
            resolution.Notes,
            state.Forte);
        RefreshConditionalCardGlows(player);
    }

    /// <summary>
    /// Removes the currently slotted notes without resolving their effects.
    /// The returned snapshot lets cards calculate rewards from what was removed.
    /// </summary>
    public static IReadOnlyList<MgrNote> RemoveAllNotes(Player player)
    {
        MgrCombatState state = MgrCombatStateStore.For(player);
        MgrNote[] removed = state.Phrase.Notes.ToArray();
        state.Phrase.Clear();
        RefreshConditionalCardGlows(player);
        MgrNoteVisuals.Show(
            player,
            state.Phrase.Notes,
            state.Phrase.Capacity,
            state.Forte,
            clearAfterDelay: false);
        return removed;
    }

    /// <summary>
    /// Removes notes from the right edge without resolving their effects.
    /// Cards can require a full amount before calling this method when partial
    /// payment is not allowed.
    /// </summary>
    public static IReadOnlyList<MgrNote> RemoveRightmostNotes(Player player, int count)
    {
        MgrCombatState state = MgrCombatStateStore.For(player);
        IReadOnlyList<MgrNote> removed = state.Phrase.RemoveRightmost(count);
        if (removed.Count == 0)
            return removed;

        RefreshConditionalCardGlows(player);
        MgrNoteVisuals.Show(
            player,
            state.Phrase.Notes,
            state.Phrase.Capacity,
            state.Forte,
            clearAfterDelay: false);
        return removed;
    }

    /// <summary>
    /// Generates a copy of the rightmost note through the ordinary channeling
    /// path. It therefore respects Double Notes and all normal chord handling.
    /// </summary>
    public static async Task<bool> CopyRightmostNote(
        PlayerChoiceContext choiceContext,
        Player player) =>
        await CopyRightmostNotes(choiceContext, player, 1) > 0;

    /// <summary>
    /// Copies a snapshot of the requested rightmost notes in their original
    /// left-to-right order. Snapshotting prevents a completed chord from changing
    /// which notes belong to this copy operation.
    /// </summary>
    public static async Task<int> CopyRightmostNotes(
        PlayerChoiceContext choiceContext,
        Player player,
        int count)
    {
        if (count <= 0)
            return 0;

        MgrCombatState state = MgrCombatStateStore.For(player);
        NoteKind[] snapshot = state.Phrase.Notes
            .TakeLast(count)
            .Select(note => note.Kind)
            .ToArray();
        if (snapshot.Length == 0)
            return 0;

        int copied = 0;
        foreach (NoteKind kind in snapshot)
        {
            if (ShouldStopNoteSequence(player))
                break;

            await ChannelNote(choiceContext, player, kind);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Copies a snapshot of every currently slotted note through the ordinary
    /// channeling path. Taking the snapshot first is important: copied notes may
    /// complete a chord, but notes created during that resolution must not be
    /// appended to this same copy operation.
    /// </summary>
    public static async Task<int> CopyAllNotes(
        PlayerChoiceContext choiceContext,
        Player player,
        int copySets = 1)
    {
        if (copySets <= 0)
            return 0;

        NoteKind[] snapshot = MgrCombatStateStore.For(player)
            .Phrase
            .Notes
            .Select(note => note.Kind)
            .ToArray();
        if (snapshot.Length == 0)
            return 0;

        int copied = 0;
        for (int set = 0; set < copySets; set++)
        {
            foreach (NoteKind kind in snapshot)
            {
                if (ShouldStopNoteSequence(player))
                    return copied;

                if (await ChannelNote(choiceContext, player, kind))
                    copied++;
            }
        }

        return copied;
    }

    /// <summary>
    /// Replaces every currently slotted note without treating the replacement as
    /// newly generated notes and without resolving a chord. This is deliberately
    /// separate from <see cref="ChannelNote"/> so replacement effects are not
    /// doubled by Double Notes or blocked by Attack Note Silence.
    /// </summary>
    public static int ReplaceAllNotes(Player player, NoteKind replacementKind)
    {
        MgrCombatState state = MgrCombatStateStore.For(player);
        int count = state.Phrase.Notes.Count;
        if (count == 0)
            return 0;

        state.Phrase.Clear();
        for (int index = 0; index < count; index++)
            state.Phrase.Add(MgrNoteFactory.Create(replacementKind));

        state.SetForteSnapshot(player.Creature.GetPowerAmount<FortePower>());
        RefreshConditionalCardGlows(player);
        MgrNoteVisuals.Show(
            player,
            state.Phrase.Notes,
            state.Phrase.Capacity,
            state.Forte,
            clearAfterDelay: false);
        return count;
    }

    /// <summary>
    /// STS1 Starting: the phrase has no notes before the current card generates one.
    /// </summary>
    public static bool IsStarting(Player player) =>
        MgrCombatStateStore.For(player).Phrase.IsStarting;

    /// <summary>
    /// STS1 Ending: exactly one slot remains before the current card generates one.
    /// This stays correct when cards later increase or decrease slot capacity.
    /// </summary>
    public static bool IsEnding(Player player) =>
        MgrCombatStateStore.For(player).Phrase.IsEnding;

    /// <summary>
    /// Unified API for future cards that add or remove note slots. Reducing the
    /// capacity can immediately resolve one or more complete phrases.
    /// </summary>
    public static Task ChangeSlotCapacity(
        PlayerChoiceContext choiceContext,
        Player player,
        int delta)
    {
        if (delta == 0)
            return Task.CompletedTask;

        MgrCombatState state = MgrCombatStateStore.For(player);
        long requestedCapacity = (long)state.Phrase.Capacity + delta;
        int newCapacity = (int)Math.Clamp(requestedCapacity, 1L, int.MaxValue);
        return SetSlotCapacity(choiceContext, player, newCapacity);
    }

    /// <summary>
    /// Sets an exact slot capacity. Capacity is never allowed below one.
    /// </summary>
    public static async Task SetSlotCapacity(
        PlayerChoiceContext choiceContext,
        Player player,
        int capacity)
    {
        MgrCombatState state = MgrCombatStateStore.For(player);
        int newCapacity = Math.Max(1, capacity);
        if (newCapacity == state.Phrase.Capacity)
            return;

        IReadOnlyList<PhraseResolution> resolutions = state.SetPhraseCapacity(newCapacity);
        state.SetForteSnapshot(player.Creature.GetPowerAmount<FortePower>());
        RefreshConditionalCardGlows(player);
        if (resolutions.Count == 0)
        {
            MgrNoteVisuals.Show(
                player,
                state.Phrase.Notes,
                state.Phrase.Capacity,
                state.Forte,
                clearAfterDelay: false);
            return;
        }

        for (int index = 0; index < resolutions.Count; index++)
        {
            if (ShouldStopNoteSequence(player))
                break;

            PhraseResolution resolution = resolutions[index];
            int chordTriggersBefore = state.ChordTriggersThisTurn;
            bool isLastWithNoRemainder =
                index == resolutions.Count - 1 && state.Phrase.Notes.Count == 0;

            MgrAudio.PlayChord();
            MgrNoteVisuals.Show(
                player,
                resolution.Notes,
                state.Phrase.Capacity,
                state.Forte,
                clearAfterDelay: isLastWithNoRemainder,
                chordAnimationIndex: chordTriggersBefore);
            await TriggerResolvedChord(choiceContext, player, resolution.Notes, state.Forte);

            if (ShouldStopNoteSequence(player))
                break;
        }

        RefreshConditionalCardGlows(player);

        if (state.Phrase.Notes.Count > 0)
        {
            MgrNoteVisuals.Show(
                player,
                state.Phrase.Notes,
                state.Phrase.Capacity,
                state.Forte,
                clearAfterDelay: false);
        }
    }

    public override Task AfterPlayerTurnStartEarly(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (MgrCombatStateStore.TryGet(player, out MgrCombatState state))
        {
            state.ResetTurnCounters();
            player.Creature
                .GetPower<UniverseOf88KeysPower>()?
                .NotifyChordCounterChanged();
            RefreshConditionalCardGlows(player);
        }

        if (player.Character is MgrCharacter)
            MgrPerformanceStateStore.For(player).ResetTurnCounters();

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Character is MgrCharacter)
            await MgrPerformanceSystem.PerformAtTurnStart(choiceContext, player);
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (CurrentCombatState is not { } combatState)
            return Task.CompletedTask;

        foreach (Player player in combatState.Players)
        {
            if (player.Character is MgrCharacter &&
                player.Creature.Side == side)
            {
                MgrCombatStateStore.For(player).SetUnusedEnergyLastTurn(
                    player.PlayerCombatState?.Energy ?? 0m);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs immediately before Tower 2 snapshots the hand for its end-of-turn
    /// flush. Coward Rocket leaves Hand here, so the native bulk discard never
    /// includes it and the visual path is directly Hand -> Performance.
    /// </summary>
    public override async Task BeforeFlush(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Character is not MgrCharacter ||
            player.PlayerCombatState is null)
        {
            return;
        }

        CowardRocket[] rockets = player.PlayerCombatState.Hand.Cards
            .OfType<CowardRocket>()
            .ToArray();

        foreach (CowardRocket rocket in rockets)
        {
            if (rocket.Pile?.Type == PileType.Hand)
            {
                await MgrPerformanceSystem.EnqueueCardFromHand(
                    player,
                    rocket);
            }
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        foreach (CardModel lastCard in _lastPlayedCards.Values)
        {
            if (lastCard is EastOfTimeline finalTimeline)
                finalTimeline.IncreaseNotesPermanently();
        }

        _lastPlayedCards.Clear();
        MgrPerformanceSystem.ClearAll();
        MgrCombatCardMutationState.Clear();
        MgrNoteVisuals.ClearAll();
        MgrCombatStateStore.Clear();
        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // AfterDamageReceived is skipped for lethal hits by the base game.
        // AfterDamageGiven runs for every resolved hit, so this keeps MGR's
        // source totals aligned with the base game's DamageDealt counter.
        MgrRunTelemetryAccumulator.RecordOutgoingDamage(
            target,
            result,
            dealer,
            cardSource);

        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // Manimani's red lethal glow depends on live enemy HP. Damage does not
        // otherwise raise a card-model change event, so explicitly ask the
        // native hand holder to re-read its glow channels after an enemy is hurt.
        if (target.IsEnemy && result.UnblockedDamage > 0)
            RefreshManimaniHandGlows();

        return Task.CompletedTask;
    }

    private static void RefreshManimaniHandGlows()
    {
        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand is null)
            return;

        foreach (NHandCardHolder holder in hand.ActiveHolders)
        {
            if (holder.CardModel is Manimani)
                holder.UpdateCard();
        }
    }

    private static async Task TriggerResolvedChord(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<MgrNote> notes,
        int forte)
    {
        if (ShouldStopNoteSequence(player))
            return;

        MgrCombatState state = MgrCombatStateStore.For(player);
        MgrRunTelemetryAccumulator.RecordChordCompleted(player);
        int triggerCount = 1 + state.ConsumePendingChordTriggers();
        int metronomeCountedTriggerCount = triggerCount;
        Metronome? metronome = player.GetRelic<Metronome>();

        int lastTriggerBefore = state.ChordTriggersThisTurn;
        for (int index = 0; index < triggerCount; index++)
        {
            if (ShouldStopNoteSequence(player))
                break;

            MgrRunTelemetryAccumulator.RecordChordEffectTrigger(player);
            int chordTriggersBefore = state.RecordChordTrigger();
            // Base passes and repeats from external effects such as
            // Cumulonimbus advance Metronome. The pass created by Metronome
            // itself still counts for all Chord gameplay, but must not advance
            // its own next cycle, matching Pen Nib and Nunchaku reset behavior.
            if (index < metronomeCountedTriggerCount &&
                metronome?.TryDoubleCurrentChord() == true)
            {
                triggerCount++;
            }
            player.Creature
                .GetPower<UniverseOf88KeysPower>()?
                .NotifyChordCounterChanged();
            lastTriggerBefore = chordTriggersBefore;
            if (index > 0)
            {
                MgrAudio.PlayChord();
                await MgrNoteVisuals.PlayRepeatedChordTrigger(
                    player,
                    notes,
                    state.Phrase.Capacity,
                    forte,
                    chordTriggersBefore);
            }

            await MgrNoteEffects.TriggerChord(
                choiceContext,
                player,
                notes,
                forte,
                chordTriggersBefore);

            if (ShouldStopNoteSequence(player))
                break;
        }

        if (triggerCount > 1)
            MgrNoteVisuals.FinishRepeatedChordTrigger(player, lastTriggerBefore);
    }

    /// <summary>
    /// Victory can become observable before the surrounding card command has
    /// returned. Stop MGR's serialized Note pipeline at that boundary so a
    /// lethal card or Attack Note cannot hold the victory flow open with Notes
    /// and repeated Chords whose results are no longer needed.
    /// </summary>
    internal static bool ShouldStopNoteSequence(Player player)
    {
        if (player.Creature.IsDead || CombatManager.Instance.IsOverOrEnding)
            return true;

        return player.Creature.CombatState is { Enemies.Count: > 0 } combatState &&
            combatState.Enemies.All(static enemy => enemy.IsDead);
    }

    /// <summary>
    /// MGR's phrase and chord counters live outside Tower 2's immutable combat
    /// state, so changing them does not itself raise CombatStateChanged. Refresh
    /// the native hand holders explicitly so conditional glow channels are read
    /// immediately instead of waiting for an unrelated engine state update.
    /// </summary>
    private static void RefreshConditionalCardGlows(Player player)
    {
        if (NPlayerHand.Instance is { } hand)
        {
            foreach (CardModel card in PileType.Hand.GetPile(player).Cards)
            {
                if (hand.GetCardHolder(card) is NHandCardHolder holder)
                    holder.UpdateCard();
            }
        }

        MgrPerformanceVisuals.RefreshConditionalCardPreviews(player);
    }
}
