using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class CreationResonancePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/CreationResonancePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/CreationResonancePower.png");

    public override async Task AfterCardGeneratedForCombat(
        CardModel card,
        Player? creator)
    {
        if (creator?.Creature != Owner)
            return;

        NoteKind noteKind;
        try
        {
            noteKind = CardNoteResolver.Resolve(card);
        }
        catch (ArgumentException)
        {
            return;
        }

        int notes = Math.Max(0, (int)Amount);
        if (notes == 0)
            return;

        Flash();
        var choiceContext = new ThrowingPlayerChoiceContext();
        for (int index = 0; index < notes; index++)
            await MgrNoteSystem.ChannelNote(choiceContext, creator, noteKind);
    }
}
