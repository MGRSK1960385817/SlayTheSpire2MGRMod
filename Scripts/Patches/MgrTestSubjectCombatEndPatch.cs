using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Keeps Test Subject's Adaptable vote limited to its actual respawn move.
/// A stale Adaptable instance must not hold the room open after the final form.
/// </summary>
public sealed class MgrTestSubjectCombatEndPatch : IPatchMethod
{
    private const string RespawnMoveId = "RESPAWN_MOVE";

    public static string PatchId => "mgr_test_subject_combat_end";
    public static string Description =>
        "Prevents stale Test Subject revival state from blocking combat completion";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(AdaptablePower),
            nameof(AdaptablePower.ShouldStopCombatFromEnding),
            Type.EmptyTypes)
    ];

    public static void Postfix(AdaptablePower __instance, ref bool __result)
    {
        if (!__result ||
            !__instance.Owner.IsDead ||
            __instance.Owner.Monster is not TestSubject testSubject ||
            !testSubject.ShouldDisappearFromDoom)
        {
            return;
        }

        __result = string.Equals(
            testSubject.NextMove.Id,
            RespawnMoveId,
            StringComparison.Ordinal);
    }
}
