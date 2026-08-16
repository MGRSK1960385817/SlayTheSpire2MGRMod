using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class TemporaryFortePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/TemporaryFortePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/TemporaryFortePower.png");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
            return;

        if (Owner.Player is { } player)
            MgrSpringStormVfx.Hide(player);
        Flash();
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner,
            -Amount,
            Owner,
            cardSource: null);
        await PowerCmd.Remove(this);
    }
}
