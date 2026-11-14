using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "black_gold_record")]
public sealed class BlackGoldRecord : ModRelicTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new GoldVar(4)];

    public override RelicRarity Rarity => RelicRarity.Shop;

#if STS2_V107 || STS2_V111
    public override bool IsAllowed(IRunState runState) =>
        IsBeforeAct3TreasureChest(runState);
#else
    protected override bool IsAllowedInternal(IRunState runState) =>
        !IsLateInAct3(runState);
#endif

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/BlackGoldRecord.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/BlackGoldRecord_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/BlackGoldRecord.png");

    public async Task OnPerformanceEnded(Player player)
    {
        if (IsMelted)
            return;

        Flash();
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, player);
    }
}
