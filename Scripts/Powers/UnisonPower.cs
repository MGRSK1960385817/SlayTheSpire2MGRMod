using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class UnisonPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/UnisonPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/UnisonPower.png");

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner.Player is not { } player || cardPlay.Card.Owner != player)
            return;

        int notes = Math.Max(0, (int)Amount);
        if (notes == 0)
            return;

        Flash();
        for (int index = 0; index < notes; index++)
            await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, player);
    }
}
