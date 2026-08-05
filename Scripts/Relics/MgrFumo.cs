using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "mgr_fumo")]
public sealed class MgrFumo : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/MgrFumo.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/MgrFumo_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/MgrFumo.png");
}
