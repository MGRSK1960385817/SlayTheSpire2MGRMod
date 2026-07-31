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

        if (player.GetRelic<GoldenRecord>() is { } goldenRecord)
        {
            goldenRecord.Flash();
            await PlayerCmd.GainGold(1m, player);
        }

        if (player.Creature.GetPower<HappySynthesizerPower>() is { } synthesizer)
            await synthesizer.OnChordTriggered(choiceContext, notes);

        for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
        {
            MgrNote note = notes[noteIndex];
            await Trigger(
                choiceContext,
                player,
                note,
                forte,
                fastPresentation);
        }

        decimal folkRhymesBlock = player.Creature.GetPowerAmount<SatelliteGirlPower>();
        if (folkRhymesBlock > 0m)
        {
            await CreatureCmd.GainBlock(
                player.Creature,
                folkRhymesBlock,
                ValueProp.Unpowered,
                cardPlay: null,
                fast: fastPresentation);
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

                var target = player.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
                if (target is null)
                    return;

                // STS1 used THORNS damage: note damage is deliberately unpowered and
                // is not attributed to the card that completed the chord.
                await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    amount,
                    fastPresentation
                        ? ValueProp.Unpowered | ValueProp.SkipHurtAnim
                        : ValueProp.Unpowered,
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
                decimal powerNoteBlock = owner.GetPowerAmount<MindMiragePower>();
                if (powerNoteBlock > 0m)
                {
                    await CreatureCmd.GainBlock(
                        owner,
                        powerNoteBlock,
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
                return;
            }
            case NoteKind.Curse:
            {
                // Curse notes deliberately ignore Forte, but Curse Wardrobe is
                // a separate flat bonus and therefore applies afterward.
                int wardrobeBonus = Math.Max(0, (int)owner.GetPowerAmount<StainedNocturnePower>());
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
                await PowerCmd.Apply<BufferPower>(
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
