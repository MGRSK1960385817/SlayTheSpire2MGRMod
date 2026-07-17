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
        int forte)
    {
        if (player.GetRelic<GoldenRecord>() is { } goldenRecord)
        {
            goldenRecord.Flash();
            await PlayerCmd.GainGold(2m, player);
        }

        int harmonyRepeats = Math.Max(
            0,
            (int)player.Creature.GetPowerAmount<HarmonyFormPower>());
        for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
        {
            MgrNote note = notes[noteIndex];
            int repeats = 1;
            if (noteIndex == 0 || noteIndex == notes.Count - 1)
                repeats += harmonyRepeats;

            for (int repeat = 0; repeat < repeats; repeat++)
                await Trigger(choiceContext, player, note, forte);
        }

        decimal folkRhymesBlock = player.Creature.GetPowerAmount<FolkRhymesPower>();
        if (folkRhymesBlock > 0m)
        {
            await CreatureCmd.GainBlock(
                player.Creature,
                folkRhymesBlock,
                ValueProp.Unpowered,
                cardPlay: null);
        }
    }

    public static async Task Trigger(
        PlayerChoiceContext choiceContext,
        Player player,
        MgrNote note,
        int forte)
    {
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
                    ValueProp.Unpowered,
                    owner,
                    cardSource: null,
                    cardPlay: null);

                if (owner.Powers.OfType<StereophonicPower>().Any())
                {
                    decimal block = amount / 2;
                    if (block > 0m)
                        await CreatureCmd.GainBlock(owner, block, ValueProp.Unpowered, cardPlay: null);
                }
                return;
            }
            case NoteKind.Skill:
            {
                await CreatureCmd.GainBlock(owner, amount, ValueProp.Unpowered, cardPlay: null);
                if (owner.Powers.OfType<StereophonicPower>().Any() &&
                    combatState is not null &&
                    combatState.HittableEnemies.Count > 0)
                {
                    var target = player.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
                    if (target is not null)
                    {
                        await CreatureCmd.Damage(
                            choiceContext,
                            target,
                            amount,
                            ValueProp.Unpowered,
                            owner,
                            cardSource: null,
                            cardPlay: null);
                    }
                }
                return;
            }
            case NoteKind.Power:
            {
                await CardPileCmd.Draw(choiceContext, amount, player);
                decimal powerNoteBlock = owner.GetPowerAmount<PowerNoteBlockPower>();
                if (powerNoteBlock > 0m)
                {
                    await CreatureCmd.GainBlock(
                        owner,
                        powerNoteBlock,
                        ValueProp.Unpowered,
                        cardPlay: null);
                }
                if (owner.GetPowerAmount<StereophonicPlusPower>() > 0m)
                {
                    decimal doubled = amount * 2m;
                    await CreatureCmd.GainBlock(owner, doubled, ValueProp.Unpowered, cardPlay: null);
                    if (combatState is not null)
                    {
                        foreach (var target in combatState.HittableEnemies.ToArray())
                        {
                            await CreatureCmd.Damage(
                                choiceContext,
                                target,
                                doubled,
                                ValueProp.Unpowered,
                                owner,
                                cardSource: null,
                                cardPlay: null);
                        }
                    }
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
                int wardrobeBonus = Math.Max(0, (int)owner.GetPowerAmount<CurseWardrobePower>());
                await CreatureCmd.Heal(owner, amount + wardrobeBonus);
                return;
            }
            case NoteKind.Quest:
                // STS1's special Ghost note used this artwork and granted Intangible.
                // Quest is the new STS2 card type, so it becomes the direct-name carrier.
                await PowerCmd.Apply<IntangiblePower>(choiceContext, owner, amount, owner, cardSource: null);
                return;
            case NoteKind.Starry:
                await PlayerCmd.GainEnergy(amount, player);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(note), note.Kind, "Unknown MGR note kind.");
        }
    }
}
