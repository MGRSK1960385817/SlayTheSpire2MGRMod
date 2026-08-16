using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// MGR's combat-field visuals use positive Z indices so the notes and
/// Performance cards remain above the character. The potion action popup is a
/// small Top Bar child rather than an overlay screen, so give only that popup a
/// higher local draw order instead of hiding the combat presentation behind it.
/// </summary>
public sealed class MgrPotionPopupLayerPatch : IPatchMethod
{
    private const int PotionPopupZIndex = 4095;

    public static string PatchId => "mgr_potion_popup_draw_order";
    public static string Description =>
        "Keeps the potion use/discard popup above MGR combat visuals";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NPotionPopup),
            nameof(NPotionPopup._Ready),
            Type.EmptyTypes,
            MethodType.Normal)
    ];

    public static void Postfix(NPotionPopup __instance)
    {
        __instance.ZIndex = PotionPopupZIndex;
    }
}
