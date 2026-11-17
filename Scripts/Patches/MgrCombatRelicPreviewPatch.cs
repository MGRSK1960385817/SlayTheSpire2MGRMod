using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

public sealed class MgrCombatRelicPreviewPatch : IPatchMethod
{
    private static readonly Action<CombatStateTracker, string> NotifyCombatStateChanged =
        AccessTools.MethodDelegate<Action<CombatStateTracker, string>>(
            AccessTools.Method(typeof(CombatStateTracker), "NotifyCombatStateChanged", [typeof(string)]));

    public static string PatchId => "mgr_combat_relic_preview";
    public static string Description => "Refreshes combat card previews when relics change";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(Player), nameof(Player.AddRelicInternal), [typeof(RelicModel), typeof(int), typeof(bool)]),
        new(typeof(Player), nameof(Player.RemoveRelicInternal), [typeof(RelicModel), typeof(bool)]),
        new(typeof(Player), nameof(Player.MeltRelicInternal), [typeof(RelicModel)])
    ];

    public static void Postfix(Player __instance)
    {
        var combatManager = CombatManager.Instance;
        if (combatManager.IsInProgress && __instance.Creature.CombatState is not null)
        {
            // Use the native deferred refresh for both the hand and the queue.
            // Relics such as Chemical X can change card values without a power or cost event.
            NotifyCombatStateChanged(combatManager.StateTracker, nameof(MgrCombatRelicPreviewPatch));
        }
    }
}
