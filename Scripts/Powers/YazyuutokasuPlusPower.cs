using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class YazyuutokasuPlusPower : YazyuutokasuPower
{
    protected override bool CreatesUpgradedConfused => true;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/YazyuutokasuPlusPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/YazyuutokasuPlusPower.png");
}
