using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class AttackNoteSilencePower : ModPowerTemplate
{
    // This is the permanent rule installed by a Power card, not a cleansable
    // enemy debuff; marking it as a Buff prevents Artifact-style negation.
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/AttackNoteSilencePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/AttackNoteSilencePower.png");
}
