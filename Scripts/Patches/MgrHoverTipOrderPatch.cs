using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Cards;
using MGRMod.Mechanics;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Tower 2 places CardModel.ExtraHoverTips before all native and registered
/// card-keyword tips. MGR's prose-only explanatory boxes are supplementary,
/// so keep the game's normal keyword order and move only those boxes to the end.
/// </summary>
public sealed class MgrHoverTipOrderPatch : IPatchMethod
{
    public static string PatchId => "mgr_supplemental_hover_tips_last";
    public static string Description => "Places MGR supplemental card explanations after normal keywords";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(CardModel),
            nameof(CardModel.HoverTips),
            Type.EmptyTypes,
            MethodType.Getter)
    ];

    public static void Postfix(
        CardModel __instance,
        ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not MgrCard)
            return;

        List<IHoverTip> tips = __result.ToList();
        if (!tips.Any(MgrHoverTips.IsSupplemental))
            return;

        __result = tips
            .Where(tip => !MgrHoverTips.IsSupplemental(tip))
            .Concat(tips.Where(MgrHoverTips.IsSupplemental))
            .ToArray();
    }
}
