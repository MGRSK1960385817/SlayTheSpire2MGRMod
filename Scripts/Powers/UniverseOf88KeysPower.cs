using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Compatibility;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class UniverseOf88KeysPower : ModPowerTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new IntVar("CurrentDamage", 0m)]; // Used by the previous descriptions pending translation.

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/UniverseOf88KeysPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/UniverseOf88KeysPower.png");

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        DynamicVars["CurrentDamage"].BaseValue = Amount;
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
            DynamicVars["CurrentDamage"].BaseValue = Amount;

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) ||
            Owner.Player is not { } player || Owner.CombatState is not { } combatState ||
            MgrNoteSystem.ShouldStopNoteSequence(player) || Amount <= 0)
        {
            return;
        }

        bool starting = MgrNoteSystem.IsStarting(player);
        bool ending = MgrNoteSystem.IsEnding(player);
        if (starting)
        {
            Flash();
            Creature[] enemies = combatState.HittableEnemies.ToArray();
            await MgrAbilityVfx.PlayUniverseOf88Keys(player, enemies, (Amount + 1) / 2);
            foreach (Creature enemy in enemies)
            {
                if (MgrNoteSystem.ShouldStopNoteSequence(player))
                    break;

                await MgrCrossVersionApi.Damage(
                    choiceContext, enemy, Amount, ValueProp.Unpowered,
                    Owner, cardSource: null, cardPlay: null);
            }
        }

        if (ending && !MgrNoteSystem.ShouldStopNoteSequence(player))
        {
            if (!starting)
                Flash();
            await MgrAbilityVfx.PlayUniverseOf88Keys(player, [Owner], (Amount + 1) / 2);
            if (!MgrNoteSystem.ShouldStopNoteSequence(player))
                await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, cardPlay: null);
        }
    }
}
