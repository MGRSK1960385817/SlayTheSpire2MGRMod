using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Cards;
using STS2RitsuLib.Patching.Models;

namespace SlayTheSpire2MGRMod.Patches;

/// <summary>
/// Adds the combat-only Performance modifier to any card's rendered text. The
/// base game performs the equivalent presentation automatically for Hidden
/// Gem's BaseReplayCount; Performance needs its own line because it is not Replay.
/// </summary>
public sealed class MgrPerformanceDescriptionPatch : IPatchMethod
{
    public static string PatchId => "mgr_performance_description";
    public static string Description => "Shows combat-only Performance on modified cards";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(CardModel),
            nameof(CardModel.GetDescriptionForPile),
            [typeof(PileType), typeof(CardModel.DescriptionPreviewType), typeof(Creature)])
    ];

    public static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance is MgrCard { IsStarryCard: true })
        {
            var starry = new LocString(
                "cards",
                "SLAY_THE_SPIRE2_MGR_MOD_CARD_STARRY_TYPE_LINE");
            string styled = $"[sine][color=#b96cff]{starry.GetFormattedText()}[/color][/sine]";
            __result = string.IsNullOrWhiteSpace(__result)
                ? styled
                : $"{styled}\n{__result}";
        }

        int amount = MgrPerformanceModifierState.GetAdditionalPerformances(__instance);
        if (amount <= 0)
            return;

        var line = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_COMBAT_PERFORMANCE_BONUS");
        line.Add("Times", amount);
        string formatted = $"[purple]{line.GetFormattedText()}[/purple]";
        __result = string.IsNullOrWhiteSpace(__result)
            ? formatted
            : $"{__result}\n{formatted}";
    }
}
