using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

/// <summary>
/// Player-side ability: every triggered Status Note applies one stack of
/// Watching U to every enemy for each stack of this power.
/// </summary>
[RegisterPower]
public sealed class WatchingUPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/cards/WatchingU.png",
        BigIconPath: $"{Entry.ResPath}/images/cards/WatchingU.png");
}
