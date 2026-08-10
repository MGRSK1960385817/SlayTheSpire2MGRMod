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
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Powers;
using SlayTheSpire2MGRMod.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Global combat listener that generates exactly one note for every resolved CardPlay
/// owned by an MGR player. Auto-play and every Replay resolution are intentionally included.
/// </summary>
[RegisterSingleton]
public sealed class MgrNoteSystem : HookedSingletonModel
{
    private readonly Dictionary<Player, CardModel> _lastPlayedCards = [];

    public MgrNoteSystem() : base(HookType.Combat)
    {
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

            if (player.GetRelic<MiniMicrophone>() is not { IsUsedUp: false } relic)
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
                firstTimeline.IncreaseNotesPermanently(1m);

            _lastPlayedCards[player] = cardPlay.Card;
        }
    }

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

    /// <summary>
    /// Unified entry point for card plays, discard-based generation and future card effects.
    /// Filling the current capacity resolves a chord and triggers its notes from left to right.
    /// </summary>
    public static async Task<bool> ChannelNote(
        PlayerChoiceContext choiceContext,
        Player player,
        NoteKind kind)
    {
        if (kind == NoteKind.Attack &&
            player.Creature.GetPowerAmount<AttackNoteSilencePower>() > 0m)
        {
            return false;
        }

        int copies = player.Creature.GetPowerAmount<DoubleNotesPower>() > 0m ? 2 : 1;
        for (int copy = 0; copy < copies; copy++)
            await ChannelSingleNote(choiceContext, player, kind);
        return true;
    }

    /// <summary>
    /// STS1 "Improvise": generates one weighted random basic note. The original
    /// distribution is preserved while mapping its old note names to STS2's
    /// direct card-type names: Attack 35%, Skill 35%, Status 8%, Power 17%,
    /// and Curse 5%.
    /// </summary>
    public static Task ChannelRandomBasicNote(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        int roll = player.RunState.Rng.CombatCardGeneration.NextInt(0, 100);
        NoteKind kind = roll switch
        {
            < 38 => NoteKind.Attack,
            < 72 => NoteKind.Skill,
            < 79 => NoteKind.Status,
            < 95 => NoteKind.Power,
            _ => NoteKind.Curse
        };

        return ChannelNote(choiceContext, player, kind);
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

        if (resolution is null)
            return;

        MgrAudio.PlayChord();
        await TriggerResolvedChord(choiceContext, player, resolution.Notes, state.Forte);
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

        foreach (NoteKind kind in snapshot)
            await ChannelNote(choiceContext, player, kind);

        return snapshot.Length;
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
        }

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

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (MgrCombatStateStore.TryGet(player, out MgrCombatState state))
        {
            state.ResetTurnCounters();
            RefreshConditionalCardGlows(player);
        }

        if (player.Character is MgrCharacter)
        {
            MgrPerformanceStateStore.For(player).ResetTurnCounters();
            await MgrPerformanceSystem.PerformAtTurnStart(choiceContext, player);
        }
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
                finalTimeline.IncreaseNotesPermanently(1m);
        }

        _lastPlayedCards.Clear();
        MgrPerformanceSystem.ClearAll();
        MgrCombatCardMutationState.Clear();
        MgrNoteVisuals.ClearAll();
        MgrCombatStateStore.Clear();
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
        // Manimani's lethal glow depends on live enemy HP. Damage does not
        // otherwise raise a card-model change event, so explicitly ask the
        // native hand holder to re-read ShouldGlowGold after an enemy is hurt.
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
        MgrCombatState state = MgrCombatStateStore.For(player);
        int triggerCount = 1 + state.ConsumePendingChordTriggers();
        if (player.GetRelic<Metronome>()?.TryDoubleCurrentChord() == true)
            triggerCount++;

        for (int index = 0; index < triggerCount; index++)
        {
            int chordTriggersBefore = state.RecordChordTrigger();
            await MgrNoteEffects.TriggerChord(
                choiceContext,
                player,
                notes,
                forte,
                chordTriggersBefore);
        }
    }

    /// <summary>
    /// MGR's phrase and chord counters live outside Tower 2's immutable combat
    /// state, so changing them does not itself raise CombatStateChanged. Refresh
    /// the native hand holders explicitly so ShouldGlowGoldInternal is re-read
    /// immediately instead of waiting for an unrelated engine state update.
    /// </summary>
    private static void RefreshConditionalCardGlows(Player player)
    {
        if (NPlayerHand.Instance is not { } hand)
            return;

        foreach (CardModel card in PileType.Hand.GetPile(player).Cards)
        {
            if (hand.GetCardHolder(card) is NHandCardHolder holder)
                holder.UpdateCard();
        }
    }
}
