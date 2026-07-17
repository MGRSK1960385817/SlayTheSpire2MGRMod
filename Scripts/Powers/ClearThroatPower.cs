using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class ClearThroatPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/ClearThroatPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/ClearThroatPower.png");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.CombatState is not { } combatState)
            return;

        Creature? target = Owner.Player?.RunState.Rng.CombatTargets.NextItem(
            combatState.HittableEnemies);
        if (target is null)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            Amount,
            ValueProp.Unpowered,
            Owner,
            cardSource: null,
            cardPlay: null);
    }
}
