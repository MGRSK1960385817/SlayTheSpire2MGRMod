using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "golden_record")]
public sealed class GoldenRecord : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/GoldenRecord.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/GoldenRecord_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/GoldenRecord.png");
}
