using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MGRMod.Mechanics;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Makes Mad Science honor Imagine/Create's combat-only type replacement.
/// Mad Science computes Type from its event configuration instead of using
/// CardModel's backing field, so changing only that field would otherwise
/// throw while the five type candidates are being built.
/// </summary>
public sealed class MgrDynamicCardTypeOverridePatch : IPatchMethod
{
    public static string PatchId => "mgr_dynamic_card_type_override";
    public static string Description =>
        "Applies MGR combat type replacements to dynamic event cards";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(MadScience), "get_Type", Type.EmptyTypes)
    ];

    public static void Postfix(MadScience __instance, ref CardType __result)
    {
        if (MgrCardTypeOverrideState.TryGet(__instance, out CardType type))
            __result = type;
    }
}
