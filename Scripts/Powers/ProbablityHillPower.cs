using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class ProbablityHillPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/cards/ProbablityHill.png",
        BigIconPath: $"{Entry.ResPath}/images/cards/ProbablityHill.png");

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player || !Owner.IsAlive)
        {
            return;
        }

        Flash();
        NoteKind kind = CardNoteResolver.Resolve(cardPlay.Card);
        Player player = Owner.Player ?? throw new InvalidOperationException(
            "Probablity Hill must be owned by a player creature.");
        await MgrNoteSystem.ChannelNote(choiceContext, player, kind);
    }
}
