using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public class StereophonicPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/StereophonicPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/StereophonicPower.png");
}

[RegisterPower]
public sealed class StereophonicPlusPower : StereophonicPower
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/StereophonicPlusPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/StereophonicPlusPower.png");
}
