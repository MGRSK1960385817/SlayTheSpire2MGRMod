using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class SixthSensePower : ModPowerTemplate
{
    private const int CardsPerStack = 2;
    private const int RequiredCost = 1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/SixthSensePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/SixthSensePower.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner)
            return;

        int cardCount = checked(Math.Max(0, (int)Amount) * CardsPerStack);
        if (cardCount == 0)
            return;

        Flash();
        IEnumerable<CardModel> drawn = await CardPileCmd.Draw(
            choiceContext,
            cardCount,
            player);
        CardModel[] cardsToDiscard = drawn
            .Where(card =>
                card.Pile?.Type == PileType.Hand &&
                card.EnergyCost.GetResolved() != RequiredCost)
            .ToArray();
        await MgrDiscardPresentation.DiscardWithPreview(
            choiceContext,
            cardsToDiscard);
    }
}
