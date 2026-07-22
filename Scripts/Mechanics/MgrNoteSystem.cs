using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheSpire2MGRMod.Characters;
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
    public MgrNoteSystem() : base(HookType.Combat)
    {
    }

    public override async Task BeforeCombatStart()
    {
        MgrPerformanceSystem.ClearAll();
        MgrCombatCardMutationState.Clear();
        MgrNoteVisuals.ClearAll();
        MgrCombatStateStore.Clear();

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

            if (player.GetRelic<Fumo>() is { IsUsedUp: false } fumo)
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

            if (player.GetRelic<WeatheredPlectrum>() is { } plectrum)
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

        await ChannelNote(choiceContext, player, CardNoteResolver.Resolve(cardPlay.Card));
        MgrPerformanceSystem.ObserveResolvedCardPlay(cardPlay);
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card.Owner.Character is MgrCharacter &&
            MgrPerformanceSystem.IsPerformanceCard(card) &&
            !MgrPerformanceSystem.IsCompletingPerformance(card))
        {
            return new CardLocation(card.Owner, PileType.Play, CardPilePosition.Bottom);
        }

        return location;
    }

    /// <summary>
    /// Unified entry point for card plays, discard-based generation and future card effects.
    /// Filling the current capacity resolves a chord and triggers its notes from left to right.
    /// </summary>
    public static async Task ChannelNote(
        PlayerChoiceContext choiceContext,
        Player player,
        NoteKind kind)
    {
        if (kind == NoteKind.Attack &&
            player.Creature.GetPowerAmount<AttackNoteSilencePower>() > 0m)
        {
            return;
        }

        int copies = player.Creature.GetPowerAmount<DoubleNotesPower>() > 0m ? 2 : 1;
        for (int copy = 0; copy < copies; copy++)
            await ChannelSingleNote(choiceContext, player, kind);
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
            < 35 => NoteKind.Attack,
            < 70 => NoteKind.Skill,
            < 78 => NoteKind.Status,
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
        MgrNoteVisuals.Show(
            player,
            state.Phrase.Notes,
            state.Phrase.Capacity,
            state.Forte,
            clearAfterDelay: false);
        return removed;
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
            state.ResetTurnCounters();

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

    public override Task AfterCombatEnd(CombatRoom room)
    {
        MgrPerformanceSystem.ClearAll();
        MgrCombatCardMutationState.Clear();
        MgrNoteVisuals.ClearAll();
        MgrCombatStateStore.Clear();
        return Task.CompletedTask;
    }

    private static async Task TriggerResolvedChord(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<MgrNote> notes,
        int forte)
    {
        MgrCombatState state = MgrCombatStateStore.For(player);
        int triggerCount = 1 + state.ConsumePendingChordTriggers();
        if (player.GetRelic<DecennialMetronome>()?.TryDoubleCurrentChord() == true)
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
}
