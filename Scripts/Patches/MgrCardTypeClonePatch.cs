using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Patching.Models;

namespace SlayTheSpire2MGRMod.Patches;

/// <summary>
/// Keeps the combat type replacement when another effect clones the modified card.
/// </summary>
public sealed class MgrCardTypeClonePatch : IPatchMethod
{
    public static string PatchId => "mgr_card_type_clone";
    public static string Description => "Copies combat card type replacement to clones";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CardModel), nameof(CardModel.CreateClone), Type.EmptyTypes)
    ];

    public static void Postfix(CardModel __instance, CardModel __result) =>
        MgrCardTypeOverrideState.Copy(__instance, __result);
}
