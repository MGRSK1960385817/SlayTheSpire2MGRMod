using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "fumo")]
public sealed class Fumo : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/Fumo.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/Fumo_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/Fumo.png");
}
