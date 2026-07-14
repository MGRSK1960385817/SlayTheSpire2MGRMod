using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
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

            MgrCombatState state = MgrCombatStateStore.For(player);
            state.SetForteSnapshot(player.Creature.GetPowerAmount<FortePower>());
            MgrNoteVisuals.Show(
                player,
                state.Phrase.Notes,
                state.Phrase.Capacity,
                state.Forte,
                clearAfterDelay: false);

            if (player.GetRelic<MiniMicrophone>() is not { IsUsedUp: false } relic)
                continue;

            relic.Flash();
            await ChannelNote(choiceContext, player, NoteKind.Attack);
            await ChannelNote(choiceContext, player, NoteKind.Skill);
            await ChannelNote(choiceContext, player, NoteKind.Power);
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay);

        var player = cardPlay.Card.Owner;
        if (player.Character is not MgrCharacter)
            return Task.CompletedTask;

        return ChannelNote(choiceContext, player, CardNoteResolver.Resolve(cardPlay.Card));
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
        MgrNote note = MgrNoteFactory.Create(kind);
        MgrCombatState state = MgrCombatStateStore.For(player);
        state.SetForteSnapshot(player.Creature.GetPowerAmount<FortePower>());
        PhraseResolution? resolution = state.AddNote(note);

        MgrAudio.PlayNoteChannel();

        // Like the Defect's OrbQueue/NOrbManager split, state owns the notes and this
        // adapter only mirrors them into Godot nodes. Completed chords remain visible
        // briefly before the current display slots return to their empty outlines.
        MgrNoteVisuals.Show(
            player,
            resolution?.Notes ?? state.Phrase.Notes,
            state.Phrase.Capacity,
            state.Forte,
            clearAfterDelay: resolution is not null);

        if (resolution is null)
            return;

        MgrAudio.PlayChord();
        await MgrNoteEffects.TriggerChord(choiceContext, player, resolution.Notes, state.Forte);
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
            bool isLastWithNoRemainder =
                index == resolutions.Count - 1 && state.Phrase.Notes.Count == 0;

            MgrAudio.PlayChord();
            MgrNoteVisuals.Show(
                player,
                resolution.Notes,
                state.Phrase.Capacity,
                state.Forte,
                clearAfterDelay: isLastWithNoRemainder);
            await MgrNoteEffects.TriggerChord(
                choiceContext,
                player,
                resolution.Notes,
                state.Forte);
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

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (MgrCombatStateStore.TryGet(player, out MgrCombatState state))
            state.ResetTurnCounters();

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        MgrNoteVisuals.ClearAll();
        MgrCombatStateStore.Clear();
        return Task.CompletedTask;
    }
}
