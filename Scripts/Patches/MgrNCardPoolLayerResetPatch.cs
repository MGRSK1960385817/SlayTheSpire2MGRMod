using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MGRMod.Mechanics;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Defensive backstop for the shared native NCard pool. Tower 2 resets common
/// transforms and modulation in OnReturnedFromPool, but currently leaves the
/// CanvasItem ordering fields untouched.
/// </summary>
public sealed class MgrNCardPoolLayerResetPatch : IPatchMethod
{
    public static string PatchId => "mgr_ncard_pool_layer_reset";
    public static string Description => "Resets stale NCard canvas ordering after pool reuse";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NCard),
            nameof(NCard.OnReturnedFromPool),
            Type.EmptyTypes)
    ];

    public static void Postfix(NCard __instance)
    {
        MgrCardNodePoolSafety.NormalizeCanvasOrdering(__instance);
    }
}
