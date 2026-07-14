using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

/// <summary>
/// STS2-native version of MGR's Forte. Notes read its current amount when a chord
/// resolves; it does not inherit Defect Focus or any orb behavior.
/// </summary>
[RegisterPower]
public sealed class FortePower : ModPowerTemplate
{
    public override PowerType Type => Amount >= 0 ? PowerType.Buff : PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/Forte.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/Forte.png");
}
