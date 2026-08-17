using MegaCrit.Sts2.Core.Commands;
using MGRMod.Characters;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Applies one shared gain to MGR Studio one-shots, including character select,
/// which the base game sends directly to SfxCmd instead of MgrAudio.
/// </summary>
public sealed class MgrAudioVolumePatch : IPatchMethod
{
    public static string PatchId => "mgr_studio_event_volume_gain";
    public static string Description =>
        "Applies the configured gain to event:/MGR one-shot playback";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(SfxCmd),
            nameof(SfxCmd.Play),
            [typeof(string), typeof(float)])
    ];

    public static void Prefix(string sfx, ref float volume)
    {
        if (!MgrAudio.IsMgrEvent(sfx))
            return;

        volume = MgrAudio.ApplyEventVolumeGain(sfx, volume);
    }
}
