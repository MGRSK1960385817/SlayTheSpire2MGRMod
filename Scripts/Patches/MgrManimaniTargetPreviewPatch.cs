using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MGRMod.Cards;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Lets Manimani use Tower 2's native target-preview lifecycle without
/// changing any global NCard rendering behavior. The ordinary preview update
/// runs first so damage already includes Strength, Vulnerable, enchantments and
/// other hooks before the lethal portrait is selected.
/// </summary>
public sealed class MgrManimaniTargetPreviewPatch : IPatchMethod
{
    public static string PatchId => "mgr_manimani_target_preview";
    public static string Description =>
        "Switches Manimani art and text while targeting a valid Fatal enemy";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NCard),
            nameof(NCard.SetPreviewTarget),
            [typeof(Creature)])
    ];

    public static void Postfix(NCard __instance, Creature? creature)
    {
        if (__instance.Model is not Manimani manimani ||
            !manimani.SetFatalPreview(creature))
        {
            return;
        }

        __instance.UpdateVisuals(
            __instance.DisplayingPile,
            CardPreviewMode.Normal);
    }
}
