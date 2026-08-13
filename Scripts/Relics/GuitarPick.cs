using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "guitar_pick")]
public sealed class GuitarPick : ModRelicTemplate
{
    public const decimal BlockPerChord = 1m;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/GuitarPick.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/GuitarPick_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/GuitarPick.png");
}
