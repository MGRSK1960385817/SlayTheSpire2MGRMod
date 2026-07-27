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
        MgrCard? mgrCard = __instance as MgrCard;
        string? starryText = null;
        if (mgrCard is { IsStarryCard: true })
        {
            var starry = new LocString(
                "cards",
                "SLAY_THE_SPIRE2_MGR_MOD_CARD_STARRY_TYPE_LINE");
            starryText = $"[sine][color=#b96cff]{starry.GetFormattedText()}[/color][/sine]";
        }

        int amount = MgrPerformanceModifierState.GetAdditionalPerformances(__instance);
        string? addedPerformanceText = null;
        if (amount > 0)
        {
            var line = new LocString(
                "cards",
                "SLAY_THE_SPIRE2_MGR_MOD_CARD_COMBAT_PERFORMANCE_BONUS");
            line.Add("Times", amount);
            addedPerformanceText = line.GetFormattedText();
        }

        // Identity mechanics share the first line. Native MGR Performance cards
        // already print their value in the body, so Starry joins that line; a
        // combat-added Performance value is prepended here instead.
        if (starryText is not null && mgrCard!.InitialPerformanceTurns > 0)
        {
            __result = string.IsNullOrWhiteSpace(__result)
                ? starryText
                : $"{starryText} {__result}";
            return;
        }

        string? identityLine = (starryText, addedPerformanceText) switch
        {
            (not null, not null) => $"{starryText} {addedPerformanceText}",
            (not null, null) => starryText,
            (null, not null) => addedPerformanceText,
            _ => null
        };
        if (identityLine is not null)
        {
            __result = string.IsNullOrWhiteSpace(__result)
                ? identityLine
                : $"{identityLine}\n{__result}";
        }
    }
}
