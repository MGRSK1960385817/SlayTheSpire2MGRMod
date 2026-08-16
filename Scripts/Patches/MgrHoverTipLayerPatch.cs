using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Native hover-tip sets normally render at Z 0. MGR's Performance rack uses
/// positive combat Z indices, so relic hover text can otherwise be covered by
/// queued cards. Raise only the transient hover-tip node; relic detail screens
/// and the Performance rack keep their existing layers and input behavior.
/// </summary>
public sealed class MgrHoverTipLayerPatch : IPatchMethod
{
    private const int HoverTipZIndex = 4095;

    public static string PatchId => "mgr_hover_tip_draw_order";
    public static string Description =>
        "Keeps hover-tip text above MGR combat presentation";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NHoverTipSet),
            nameof(NHoverTipSet._Ready),
            Type.EmptyTypes,
            MethodType.Normal)
    ];

    public static void Postfix(NHoverTipSet __instance)
    {
        __instance.ZAsRelative = false;
        __instance.ZIndex = HoverTipZIndex;
    }
}
