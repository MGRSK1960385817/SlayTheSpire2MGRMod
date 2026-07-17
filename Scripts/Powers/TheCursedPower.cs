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
public sealed class TheCursedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/TheCursedPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/TheCursedPower.png");

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner || card.Type != CardType.Curse || Owner.Player is not { } player)
            return;

        int triggers = Math.Max(0, (int)Amount);
        if (triggers == 0)
            return;

        Flash();
        await CardPileCmd.Draw(choiceContext, triggers, player);
        for (int index = 0; index < triggers; index++)
            await MgrNoteSystem.ChannelNote(choiceContext, player, NoteKind.Curse);
    }
}
