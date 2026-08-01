using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
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
    private static readonly CardKeyword[] CompactTerminalKeywords =
    [
        CardKeyword.Retain,
        CardKeyword.Ethereal,
        CardKeyword.Exhaust,
        CardKeyword.Innate
    ];

    private static readonly string[] NamedColorTags =
    [
        "gold", "blue", "green", "red", "purple", "orange"
    ];

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
            if (__instance is CubicPrism)
            {
                // X-cost Performance owns the first identity line already.
                // Replace that line with one combined expression instead of
                // prepending a contradictory fixed "Performance 1" line.
                var combinedLine = new LocString(
                    "cards",
                    "SLAY_THE_SPIRE2_MGR_MOD_CARD_CUBIC_PRISM_PERFORMANCE_BONUS");
                combinedLine.Add("Times", amount);
                string combinedText = combinedLine.GetFormattedText();
                int firstLineBreak = __result.IndexOf('\n');
                __result = firstLineBreak >= 0
                    ? combinedText + __result[firstLineBreak..]
                    : combinedText;
            }
            else
            {
                var line = new LocString(
                    "cards",
                    "SLAY_THE_SPIRE2_MGR_MOD_CARD_COMBAT_PERFORMANCE_BONUS");
                line.Add("Times", amount);
                addedPerformanceText = line.GetFormattedText();
            }
        }

        // Identity mechanics share the first line. Native MGR Performance cards
        // already print their value in the body, so Starry joins that line; a
        // combat-added Performance value is prepended here instead.
        if (starryText is not null && mgrCard!.InitialPerformanceTurns > 0)
        {
            string separator = mgrCard is LonelyUniverse ? "\n" : " ";
            __result = string.IsNullOrWhiteSpace(__result)
                ? starryText
                : $"{starryText}{separator}{__result}";
        }
        else
        {
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

        if (mgrCard is not null)
        {
            CompactStarryRetainLine(mgrCard, starryText, ref __result);
            CompactTerminalKeywordLines(mgrCard, ref __result);
        }

        if (__instance is Pale)
            __result = FormatPaleDescription(__result);
    }

    private static void CompactStarryRetainLine(
        MgrCard card,
        string? starryText,
        ref string description)
    {
        if (starryText is null || !card.Keywords.Contains(CardKeyword.Retain))
            return;

        string retainText = CardKeyword.Retain.GetCardText().Trim();
        if (string.IsNullOrWhiteSpace(retainText))
            return;

        List<string> lines = description.Split('\n').ToList();
        int starryIndex = lines.FindIndex(line => line.Trim() == starryText);
        int retainIndex = lines.FindIndex(line => line.Trim() == retainText);
        if (starryIndex < 0 || retainIndex < 0 || starryIndex == retainIndex)
            return;

        lines[starryIndex] = $"{lines[starryIndex].Trim()} {retainText}";
        lines.RemoveAt(retainIndex);
        description = string.Join('\n', lines);
    }

    private static void CompactTerminalKeywordLines(
        MgrCard card,
        ref string description)
    {
        if (CompactTerminalKeywords.Count(card.Keywords.Contains) < 2)
            return;

        var keywordTexts = CompactTerminalKeywords
            .Where(card.Keywords.Contains)
            .Select(keyword => keyword.GetCardText().Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToHashSet(StringComparer.Ordinal);
        if (keywordTexts.Count < 2)
            return;

        List<string> lines = description.Split('\n').ToList();
        var matchedLines = lines
            .Select((line, index) => (Text: line.Trim(), Index: index))
            .Where(item => keywordTexts.Contains(item.Text))
            .ToList();
        if (matchedLines.Count < 2)
            return;

        int insertAt = matchedLines.Min(item => item.Index);
        string compactLine = string.Join(
            " ",
            matchedLines.OrderBy(item => item.Index).Select(item => item.Text));
        foreach (int index in matchedLines.Select(item => item.Index).OrderDescending())
            lines.RemoveAt(index);

        lines.Insert(Math.Min(insertAt, lines.Count), compactLine);
        description = string.Join('\n', lines);
    }

    private static string FormatPaleDescription(string description)
    {
        foreach (string tag in NamedColorTags)
        {
            description = description
                .Replace($"[{tag}]", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace($"[/{tag}]", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return $"[sine][color=#8a8a8a]{description}[/color][/sine]";
    }
}
