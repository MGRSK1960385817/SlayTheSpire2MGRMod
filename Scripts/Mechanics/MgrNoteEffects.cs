using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Powers;
using MGRMod.Compatibility;
using MGRMod.Relics;
using MGRMod.Telemetry;

namespace MGRMod.Mechanics;

/// <summary>
/// Tower-2 command implementation of the original MGR note effects.
/// </summary>
public static class MgrNoteEffects
{
    internal static int GetCurseHealingAmount(Player sourcePlayer, int baseAmount)
    {
        decimal nocturneBonus = Math.Max(
            0m,
            sourcePlayer.Creature.GetPowerAmount<StainedNocturnePower>());
        decimal total = Math.Max(0, baseAmount) + nocturneBonus;
        return (int)Math.Min(total, int.MaxValue);
    }

    public static async Task TriggerChord(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<MgrNote> notes,
        int forte,
        int chordTriggersBefore)
    {
        if (MgrNoteSystem.ShouldStopNoteSequence(player))
            return;

        // Standard commands deliberately spend time on hit/block/heal feedback.
        // Once a turn contains several chord passes, use their supported fast
        // presentation paths while preserving the same hooks and game state.
        bool containsOmnia = notes.Any(
            static note => note.Kind == NoteKind.OmniaNote);
        bool fastPresentation =
            containsOmnia ||
            chordTriggersBefore >=
            MgrVisualTuning.Notes.FastChordCommandThreshold;

        if (player.GetRelic<GuitarPick>() is { } guitarPick)
        {
            guitarPick.PulseLightly();
            await CreatureCmd.GainBlock(
                player.Creature,
                GuitarPick.BlockPerChord,
                ValueProp.Unpowered,
                cardPlay: null,
                fast: fastPresentation);
        }

        if (player.Creature.GetPower<PrismaticPower>() is { } synthesizer)
            await synthesizer.OnChordTriggered(choiceContext, notes);

        for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
        {
            if (MgrNoteSystem.ShouldStopNoteSequence(player))
                break;

            MgrNote note = notes[noteIndex];
            // Samsara reacts to an actual Attack Note being consumed by this
            // Chord. Omnia reproduces the Attack Note's effect, but is not an
            // Attack Note itself and therefore does not satisfy this trigger.
            if (note.Kind == NoteKind.Attack &&
                player.Creature.GetPower<SamsaraPower>() is { } samsara)
            {
                samsara.Flash();
                await PowerCmd.Apply<VigorPower>(
                    choiceContext,
                    player.Creature,
                    samsara.Amount,
                    player.Creature,
                    cardSource: null);
            }

            await Trigger(
                choiceContext,
                player,
                note,
                forte,
                fastPresentation);

            if (MgrNoteSystem.ShouldStopNoteSequence(player))
                break;

            // Give It to You shares beneficial Note effects with the chosen
            // teammate. Attack and Status Notes are global/offensive effects,
            // so sharing them means resolving their effect one additional time.
            GiveItToYouPower[] sharingPowers = player.Creature.Powers
                .OfType<GiveItToYouPower>()
                .ToArray();
            foreach (GiveItToYouPower sharingPower in sharingPowers)
            {
                if (MgrNoteSystem.ShouldStopNoteSequence(player))
                    break;

                if (!sharingPower.TryGetLivingTarget(out Player target))
                    continue;

                sharingPower.Flash();
                await TriggerShared(
                    choiceContext,
                    player,
                    target,
                    note,
                    forte,
                    fastPresentation);
            }
        }

    }

    private static async Task TriggerShared(
        PlayerChoiceContext choiceContext,
        Player sourcePlayer,
        Player targetPlayer,
        MgrNote note,
        int forte,
        bool fastPresentation)
    {
        if (MgrNoteSystem.ShouldStopNoteSequence(sourcePlayer))
            return;

        if (note.Kind == NoteKind.OmniaNote)
        {
            foreach (NoteKind kind in new[]
            {
                NoteKind.Attack,
                NoteKind.Skill,
                NoteKind.Power,
                NoteKind.Status,
                NoteKind.Curse,
                NoteKind.Starry
            })
            {
                if (MgrNoteSystem.ShouldStopNoteSequence(sourcePlayer))
                    break;

                await TriggerShared(
                    choiceContext,
                    sourcePlayer,
                    targetPlayer,
                    MgrNoteFactory.Create(kind),
                    forte,
                    fastPresentation);
            }
            return;
        }

        int amount = note.GetEffectAmount(forte);
        // Forte controls the Power Note's own draw amount, not independent
        // effects that react to a Power Note resolving. Keep the Power branch
        // alive at zero so Mind Mirage still grants its fixed Block.
        if (amount <= 0 && note.Kind != NoteKind.Power)
            return;

        switch (note.Kind)
        {
            case NoteKind.Attack:
            case NoteKind.Status:
                await Trigger(
                    choiceContext,
                    sourcePlayer,
                    note,
                    forte,
                    fastPresentation);
                return;

            case NoteKind.Skill:
                await CreatureCmd.GainBlock(
                    targetPlayer.Creature,
                    amount,
                    ValueProp.Unpowered,
                    cardPlay: null,
                    fast: fastPresentation);
                return;

            case NoteKind.Power:
                if (amount > 0)
                    await CardPileCmd.Draw(choiceContext, amount, targetPlayer);
                if (sourcePlayer.Creature.GetPower<MindMiragePower>() is
                    { Amount: > 0 } mindMirage)
                {
                    await CreatureCmd.GainBlock(
                        targetPlayer.Creature,
                        mindMirage.Amount,
                        ValueProp.Unpowered,
                        cardPlay: null,
                        fast: fastPresentation);
                }
                return;

            case NoteKind.Curse:
                await CreatureCmd.Heal(
                    targetPlayer.Creature,
                    GetCurseHealingAmount(sourcePlayer, amount),
                    playAnim: !fastPresentation);
                return;

            case NoteKind.Starry:
                await PlayerCmd.GainEnergy(amount, targetPlayer);
                return;

            case NoteKind.Ghost:
                await PowerCmd.Apply<IntangiblePower>(
                    choiceContext,
                    targetPlayer.Creature,
                    amount,
                    sourcePlayer.Creature,
                    cardSource: null);
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(note),
                    note.Kind,
                    "Unknown shared MGR note kind.");
        }
    }

    public static async Task Trigger(
        PlayerChoiceContext choiceContext,
        Player player,
        MgrNote note,
        int forte,
        bool fastPresentation = false)
    {
        if (MgrNoteSystem.ShouldStopNoteSequence(player))
            return;

        if (note.Kind == NoteKind.OmniaNote)
        {
            NoteKind[] componentKinds =
            [
                NoteKind.Attack,
                NoteKind.Skill,
                NoteKind.Power,
                NoteKind.Status,
                NoteKind.Curse,
                NoteKind.Starry
            ];
            foreach (NoteKind kind in componentKinds)
            {
                if (MgrNoteSystem.ShouldStopNoteSequence(player))
                    break;

                await Trigger(
                    choiceContext,
                    player,
                    MgrNoteFactory.Create(kind),
                    forte,
                    fastPresentation);
            }
            return;
        }

        int amount = note.GetEffectAmount(forte);
        // Mind Mirage is a flat reaction to the Power Note itself. It must not
        // disappear when negative Forte reduces only the Note's draw to zero.
        if (amount <= 0 && note.Kind != NoteKind.Power)
            return;

        var owner = player.Creature;
        var combatState = owner.CombatState;

        switch (note.Kind)
        {
            case NoteKind.Attack:
            {
                if (combatState is null || combatState.HittableEnemies.Count == 0)
                    return;

                ValueProp props = fastPresentation
                    ? ValueProp.Unpowered | ValueProp.SkipHurtAnim
                    : ValueProp.Unpowered;

                var target = player.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
                if (target is null)
                    return;

                // STS1 used THORNS damage: note damage is deliberately unpowered and
                // is not attributed to the card that completed the chord.
                using (MgrRunTelemetryAccumulator.BeginNoteDamage())
                {
                    await MgrCrossVersionApi.Damage(
                        choiceContext,
                        target,
                        amount,
                        props,
                        owner,
                        cardSource: null,
                        cardPlay: null);
                }

                return;
            }
            case NoteKind.Skill:
            {
                await CreatureCmd.GainBlock(
                    owner,
                    amount,
                    ValueProp.Unpowered,
                    cardPlay: null,
                    fast: fastPresentation);
                return;
            }
            case NoteKind.Power:
            {
                if (amount > 0)
                    await CardPileCmd.Draw(choiceContext, amount, player);
                if (owner.GetPower<MindMiragePower>() is { Amount: > 0 } mindMirage)
                {
                    mindMirage.Flash();
                    MgrAbilityVfx.SpawnCastBurst(
                        owner,
                        MgrAbilityVfxStyle.Mirage,
                        0.58f);
                    await CreatureCmd.GainBlock(
                        owner,
                        mindMirage.Amount,
                        ValueProp.Unpowered,
                        cardPlay: null,
                        fast: fastPresentation);
                }
                return;
            }
            case NoteKind.Status:
            {
                if (combatState is null)
                    return;

                var targets = combatState.HittableEnemies.ToList();
                if (targets.Count == 0)
                    return;

                await PowerCmd.Apply<WeakPower>(
                    choiceContext,
                    targets,
                    amount,
                    owner,
                    cardSource: null,
                    silent: fastPresentation);
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    targets,
                    amount,
                    owner,
                    cardSource: null,
                    silent: fastPresentation);

                if (owner.GetPower<WatchingUPower>() is { } watchingU &&
                    watchingU.Amount > 0m)
                {
                    watchingU.Flash();
                    foreach (var target in targets)
                    {
                        // Reuse the native gaze/eye feedback (the same VFX path
                        // used by Evil Eye) on the creature that is actually
                        // receiving Watching U. Keep this presentation beside
                        // the mark application so repeated Chord passes produce
                        // one readable eye pulse per application.
                        VfxCmd.PlayOnCreatureCenter(target, VfxCmd.gazePath);
                        MgrAbilityVfx.SpawnCastBurst(
                            target,
                            MgrAbilityVfxStyle.Seal,
                            0.56f);
                    }
                    await PowerCmd.Apply<WatchingUMarkPower>(
                        choiceContext,
                        targets,
                        watchingU.Amount,
                        owner,
                        cardSource: null,
                        silent: fastPresentation);
                }
                return;
            }
            case NoteKind.Curse:
            {
                // Curse notes deliberately ignore Forte, but Curse Wardrobe is
                // a separate flat bonus and therefore applies afterward.
                int healingAmount = GetCurseHealingAmount(player, amount);
                if (healingAmount > amount &&
                    owner.GetPower<StainedNocturnePower>() is { } nocturne)
                {
                    nocturne.Flash();
                    MgrAbilityVfx.SpawnCastBurst(
                        owner,
                        MgrAbilityVfxStyle.Nocturne,
                        0.58f);
                }
                await CreatureCmd.Heal(
                    owner,
                    healingAmount,
                    playAnim: !fastPresentation);

                return;
            }
            case NoteKind.Starry:
                await PlayerCmd.GainEnergy(amount, player);
                return;
            case NoteKind.Ghost:
                await PowerCmd.Apply<IntangiblePower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    cardSource: null);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(note), note.Kind, "Unknown MGR note kind.");
        }
    }
}
