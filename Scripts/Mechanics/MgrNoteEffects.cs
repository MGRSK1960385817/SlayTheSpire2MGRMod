using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Powers;
using MGRMod.Relics;
using MGRMod.Telemetry;

namespace MGRMod.Mechanics;

/// <summary>
/// Tower-2 command implementation of the original MGR note effects.
/// </summary>
public static class MgrNoteEffects
{
    public static async Task TriggerChord(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<MgrNote> notes,
        int forte,
        int chordTriggersBefore)
    {
        // Standard commands deliberately spend time on hit/block/heal feedback.
        // Once a turn contains several chord passes, use their supported fast
        // presentation paths while preserving the same hooks and game state.
        bool fastPresentation =
            chordTriggersBefore >=
            MgrVisualTuning.Notes.FastChordCommandThreshold;

        if (player.GetRelic<GuitarPick>() is { } guitarPick)
        {
            guitarPick.Flash();
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

            // Give It to You shares beneficial Note effects with the chosen
            // teammate. Attack and Status Notes are global/offensive effects,
            // so sharing them means resolving their effect one additional time.
            GiveItToYouPower[] sharingPowers = player.Creature.Powers
                .OfType<GiveItToYouPower>()
                .ToArray();
            foreach (GiveItToYouPower sharingPower in sharingPowers)
            {
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
        if (amount <= 0)
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
                int wardrobeBonus = sourcePlayer.Creature
                    .GetPower<StainedNocturnePower>() is { Amount: > 0 } nocturne
                    ? Math.Max(0, (int)nocturne.Amount)
                    : 0;
                await CreatureCmd.Heal(
                    targetPlayer.Creature,
                    amount + wardrobeBonus,
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
        if (amount <= 0)
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
                    await CreatureCmd.Damage(
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

                await PowerCmd.Apply<WeakPower>(choiceContext, targets, amount, owner, cardSource: null);
                await PowerCmd.Apply<VulnerablePower>(choiceContext, targets, amount, owner, cardSource: null);

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
                        cardSource: null);
                }
                return;
            }
            case NoteKind.Curse:
            {
                // Curse notes deliberately ignore Forte, but Curse Wardrobe is
                // a separate flat bonus and therefore applies afterward.
                int wardrobeBonus = 0;
                if (owner.GetPower<StainedNocturnePower>() is { Amount: > 0 } nocturne)
                {
                    wardrobeBonus = Math.Max(0, (int)nocturne.Amount);
                    nocturne.Flash();
                    MgrAbilityVfx.SpawnCastBurst(
                        owner,
                        MgrAbilityVfxStyle.Nocturne,
                        0.58f);
                }
                await CreatureCmd.Heal(
                    owner,
                    amount + wardrobeBonus,
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
