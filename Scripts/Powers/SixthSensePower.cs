using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class SixthSensePower : ModPowerTemplate
{
    private const int CardsPerStack = 2;
    private const int RequiredCost = 1;
    private const float PostHandDrawPauseSeconds = 0.5f;

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

        // AfterPlayerTurnStart runs after Tower 2's ordinary hand draw. Leave
        // a short visual beat so the starting hand settles before this power's
        // additional cards enter it.
        await Cmd.Wait(MgrVisualTiming.ScaleBlockingVisualWait(
            player,
            PostHandDrawPauseSeconds));

        // PowerModel.Flash is Tower 2's native triggered-power presentation:
        // besides flashing the power bar entry, it raises this power's BigIcon
        // over its owner. Keep it immediately before the additional draw so
        // the source of those cards is unambiguous.
        Flash();
        MgrAbilityVfx.SpawnCastBurst(
            Owner,
            MgrAbilityVfxStyle.Eye,
            0.72f);
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
