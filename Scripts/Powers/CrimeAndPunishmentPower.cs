using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class CrimeAndPunishmentPower : ModPowerTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("HpLoss", 0m)
    ];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/CrimeAndPunishmentPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/CrimeAndPunishmentPower.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner)
            return;

        int triggers = Math.Max(0, (int)Amount);
        if (triggers == 0)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            DynamicVars["HpLoss"].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner,
            cardSource: null,
            cardPlay: null);
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner,
            triggers,
            Owner,
            cardSource: null);
    }
}
