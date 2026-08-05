using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheSpire2MGRMod.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class LongDreamPower : TemporaryStrengthPower, IModPowerAssetOverrides
{
    public override AbstractModel OriginModel => ModelDb.Card<LongDream>();

    protected override bool IsPositive => false;

    public PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/LongDreamPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/LongDreamPower.png");

    public string? CustomIconPath => AssetProfile.IconPath;

    public string? CustomBigIconPath => AssetProfile.BigIconPath;
}
