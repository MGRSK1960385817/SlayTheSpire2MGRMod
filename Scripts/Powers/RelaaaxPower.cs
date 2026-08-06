using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class RelaaaxPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/RelaaaxPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/RelaaaxPower.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner)
            return;

        int noteCount = Math.Max(0, (int)Amount);
        if (noteCount == 0)
            return;

        Flash();
        for (int index = 0; index < noteCount; index++)
            await MgrNoteSystem.ChannelNote(choiceContext, player, NoteKind.Starry);

        await PowerCmd.Remove(this);
    }
}
