using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

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
        foreach (MgrNote note in notes)
            await Trigger(choiceContext, player, note, forte);
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
                return;
            }
            case NoteKind.Skill:
                await CreatureCmd.GainBlock(owner, amount, ValueProp.Unpowered, cardPlay: null);
                return;
            case NoteKind.Power:
                await CardPileCmd.Draw(choiceContext, amount, player);
                return;
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
                await PowerCmd.Apply<ArtifactPower>(choiceContext, owner, amount, owner, cardSource: null);
                return;
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
