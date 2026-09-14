using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
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
        SetGrantedBonusThisTurn(false);
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player == Owner)
            SetGrantedBonusThisTurn(false);

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        _grantedBonusThisTurn = false;
        Status = IsMelted
            ? RelicStatus.Disabled
            : RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public bool TryGrantPerformanceBonus()
    {
        if (IsMelted || _grantedBonusThisTurn)
            return false;

        SetGrantedBonusThisTurn(true);
        Flash();
        return true;
    }

    private void SetGrantedBonusThisTurn(bool granted)
    {
        AssertMutable();
        _grantedBonusThisTurn = granted;
        if (IsMelted)
        {
            Status = RelicStatus.Disabled;
            return;
        }

        Status = granted ? RelicStatus.Normal : RelicStatus.Active;
    }
}
