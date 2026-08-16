using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MGRMod.Characters;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Removes relics that should never be rolled for MGR from the player's
/// character-specific reward grab bag. Other characters keep the normal pool.
/// </summary>
public sealed class MgrRelicRewardPoolPatch : IPatchMethod
{
    public static string PatchId => "mgr_relic_reward_pool";
    public static string Description => "Removes MGR-incompatible relics from random rewards";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(RelicGrabBag),
            nameof(RelicGrabBag.Populate),
            [typeof(Player), typeof(Rng)])
    ];

    public static void Postfix(RelicGrabBag __instance, Player player)
    {
        if (player.Character is not MgrCharacter)
            return;

        ApplyMgrExclusions(__instance);
    }

    private static void ApplyMgrExclusions(RelicGrabBag grabBag)
    {
        // Add further MGR-only exclusions here, one Remove<T>() call per relic.
        grabBag.Remove<Orichalcum>();
        grabBag.Remove<Pocketwatch>();
    }
}
