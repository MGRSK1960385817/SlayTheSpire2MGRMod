using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Rooms;
using MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "guitar_pick")]
public sealed class GuitarPick : ModRelicTemplate
{
    public const decimal BlockPerChord = 1m;
    private const float LightPulseSeconds = 0.24f;

    private int _lightPulseVersion;

    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool ShouldFlashOnPlayer => false;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/GuitarPick.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/GuitarPick_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/GuitarPick.png");

    public void PulseLightly()
    {
        AssertMutable();
        int version = ++_lightPulseVersion;
        Status = RelicStatus.Active;
        TaskHelper.RunSafely(EndLightPulse(version));
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        _lightPulseVersion++;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    private async Task EndLightPulse(int version)
    {
        await Cmd.Wait(LightPulseSeconds);
        if (_lightPulseVersion == version)
            Status = RelicStatus.Normal;
    }
}
