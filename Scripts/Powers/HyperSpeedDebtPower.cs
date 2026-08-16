using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class HyperSpeedDebtPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/HyperSpeedDebtPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/HyperSpeedDebtPower.png");

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature != Owner)
            return;

        Flash();
        await PlayerCmd.LoseEnergy(Amount, player);
    }

    public override async Task AfterPlayerTurnStartEarly(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
