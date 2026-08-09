using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "perpetual_microphone")]
public sealed class PerpetualMicrophone : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/PerpetualMicrophone.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/PerpetualMicrophone_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/PerpetualMicrophone.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner)
            return;

        Flash();
        await MgrNoteSystem.ChannelNote(choiceContext, player, NoteKind.Attack);
        await MgrNoteSystem.ChannelNote(choiceContext, player, NoteKind.Skill);
        await MgrNoteSystem.ChannelNote(choiceContext, player, NoteKind.Power);
        if (player.PlayerCombatState is { TurnNumber: 1 })
            await MgrNoteSystem.ChannelNote(choiceContext, player, NoteKind.Starry);
    }
}
