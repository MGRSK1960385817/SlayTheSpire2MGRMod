using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Powers;
using SlayTheSpire2MGRMod.Relics;

namespace SlayTheSpire2MGRMod.Mechanics;

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
                await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    amount,
                    props,
                    owner,
                    cardSource: null,
                    cardPlay: null);

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

                if (owner.GetPower<MindBrandPower>() is { } mindBrand &&
                    mindBrand.Amount > 0m)
                {
                    mindBrand.Flash();
                    foreach (var target in targets)
                    {
                        // Reuse the native gaze/eye feedback (the same VFX path
                        // used by Evil Eye) on the creature that is actually
                        // receiving Mind Brand.  Keep this presentation beside
                        // the mark application so repeated Chord passes produce
                        // one readable eye pulse per application.
                        VfxCmd.PlayOnCreatureCenter(target, VfxCmd.gazePath);
                        MgrAbilityVfx.SpawnCastBurst(
                            target,
                            MgrAbilityVfxStyle.Seal,
                            0.56f);
                    }
                    await PowerCmd.Apply<MindBrandMarkPower>(
                        choiceContext,
                        targets,
                        mindBrand.Amount,
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
