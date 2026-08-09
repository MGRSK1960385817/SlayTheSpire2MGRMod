using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class NextPerformanceDiscountPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/NextPerformanceDiscountPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/NextPerformanceDiscountPower.png");

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner.Creature != Owner ||
            card.EnergyCost.CostsX ||
            card.Pile?.Type is not (PileType.Hand or PileType.Play) ||
            (MgrPerformanceStateStore.TryGet(card.Owner, out MgrPerformanceState state) &&
             state.Contains(card)) ||
            !MgrPerformanceSystem.IsPerformanceCard(card))
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - Amount);
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;
        if (cardPlay.IsAutoPlay ||
            card.Owner.Creature != Owner ||
            !MgrPerformanceSystem.IsPerformanceCard(card))
        {
            return;
        }

        Flash();
        await PowerCmd.Remove(this);
    }
}
