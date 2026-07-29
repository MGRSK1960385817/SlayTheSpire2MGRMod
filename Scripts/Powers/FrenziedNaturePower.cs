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
public sealed class FrenziedNaturePower : ModPowerTemplate
{
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/FrenziedNaturePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/FrenziedNaturePower.png");

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (_triggeredThisTurn || card.Owner.Creature != Owner || card.Type != CardType.Curse || Owner.Player is not { } player)
            return;

        int triggers = Math.Max(0, (int)Amount);
        if (triggers == 0)
            return;

        _triggeredThisTurn = true;
        Flash();
        await CardPileCmd.Draw(choiceContext, triggers, player);
    }

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner)
            _triggeredThisTurn = false;
        return Task.CompletedTask;
    }
}
