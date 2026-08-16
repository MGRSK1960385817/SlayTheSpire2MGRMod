using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "your_stage")]
public sealed class YourStage : ModRelicTemplate
{
    private bool _grantedBonusThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/YourStage.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/YourStage_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/YourStage.png");

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
