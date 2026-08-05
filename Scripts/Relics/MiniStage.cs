using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "mini_stage")]
public sealed class MiniStage : ModRelicTemplate
{
    private bool _grantedBonusThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/MiniStage.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/MiniStage_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/MiniStage.png");

    public override Task BeforeCombatStart()
    {
        _grantedBonusThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player == Owner)
            _grantedBonusThisTurn = false;

        return Task.CompletedTask;
    }

    public bool TryGrantPerformanceBonus()
    {
        if (_grantedBonusThisTurn)
            return false;

        _grantedBonusThisTurn = true;
        Flash();
        return true;
    }
}
