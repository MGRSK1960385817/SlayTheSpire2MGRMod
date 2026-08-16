using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Mechanics;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Gives a Performance Power card one visual owner. Tower 2 normally flies every
/// Power card into its owner and frees the real NCard afterward, while the MGR
/// rack needs that same node for its entrance. Suppressing only that native VFX
/// prevents both the conflicting silhouettes and a double return to the NCard
/// pool. Ordinary Power cards retain the vanilla animation unchanged.
/// </summary>
public sealed class MgrPerformancePowerCardVfxPatch : IPatchMethod
{
    public static string PatchId => "mgr_performance_power_card_vfx";
    public static string Description =>
        "Routes Performance Power cards exclusively into the Performance rack";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CardModel), "PlayPowerCardFlyVfx", Type.EmptyTypes)
    ];

    public static bool Prefix(CardModel __instance, ref Task __result)
    {
        if (__instance.Type != CardType.Power ||
            !MgrPerformanceSystem.IsPerformanceCard(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
