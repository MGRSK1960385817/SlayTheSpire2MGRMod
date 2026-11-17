using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Mechanics;
using MGRMod.Cards;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

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
        CardKeyword.Exhaust
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
                "MGR_MOD_CARD_STARRY_TYPE_LINE");
            starryText = $"[sine][color=#b96cff]{starry.GetFormattedText()}[/color][/sine]";
        }

        int printedTurns = mgrCard?.InitialPerformanceTurns ?? 0;
        int additionalTurns = MgrPerformanceModifierState.GetAdditionalPerformances(__instance);
        int effectiveTurns = MgrPerformanceSystem.GetInitialPerformanceTurns(__instance);
        string? addedPerformanceText = null;
        if (__instance is CubicPrism prism)
        {
            int floor = MgrPerformanceSystem.GetLoversPerformanceFloor(__instance);
            if (MgrPerformanceSystem.IsQueued(__instance))
            {
                ReplacePerformanceLine(ref __result, FormatPerformanceLine(effectiveTurns));
            }
            else if (prism.GetCurrentEnergyXForPreview() is { } xValue)
            {
                int previewTurns = MgrPerformanceSystem.ApplyPerformanceFloor(prism, xValue);
                ReplacePerformanceLine(ref __result, FormatPerformanceLine(previewTurns));
            }
            else if (floor > 0)
            {
                string x = additionalTurns > 0 ? $"X+[blue]{additionalTurns}[/blue]" : "X";
                var line = new LocString("cards", "MGR_MOD_CARD_COMBAT_X_PERFORMANCE_MINIMUM");
                line.Add("X", x);
                line.Add("Times", floor);
                ReplacePerformanceLine(ref __result, line.GetFormattedText());
            }
            else if (additionalTurns > 0)
            {
                var line = new LocString("cards", "MGR_MOD_CARD_CUBIC_PRISM_PERFORMANCE_BONUS");
                line.Add("Times", additionalTurns);
                ReplacePerformanceLine(ref __result, line.GetFormattedText());
            }
        }
        else if (__instance is LightSong lightSong)
        {
            if (additionalTurns > 0)
            {
                var line = new LocString("cards", "MGR_MOD_CARD_LIGHT_SONG_PERFORMANCE_BONUS");
                line.Add("Times", checked(additionalTurns + (lightSong.IsUpgraded ? 1 : 0)));
                ReplacePerformanceLine(ref __result, line.GetFormattedText());
            }
        }
        else if (printedTurns > 0 && effectiveTurns != printedTurns)
        {
            ReplacePerformanceLine(ref __result, FormatPerformanceLine(effectiveTurns));
        }
        else if (printedTurns == 0 && effectiveTurns > 0)
        {
            addedPerformanceText = FormatPerformanceLine(effectiveTurns);
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
            if (mgrCard is SpringStorm or Chorus or WhiteSouthWind or PuppetClown)
                MoveKeywordToFirstLine(mgrCard, CardKeyword.Retain, ref __result);
            if (mgrCard is GalaxyLamp)
                CompactPerformanceExhaustLine(ref __result);
            MoveInnateToFirstLine(mgrCard, ref __result);
            if (mgrCard is LightSong)
                CompactLightSongIdentityLine(ref __result);

            // Manimani's lethal-target presentation deliberately replaces its
            // complete rules face with “Thunder!”. Exhaust remains on the model
            // and therefore still controls gameplay; only its automatically
            // appended card-text line is hidden during this transient preview.
            if (mgrCard is Manimani { IsFatalPreviewActive: true })
                RemoveRenderedKeyword(CardKeyword.Exhaust, ref __result);

            FormatStarryNoteText(ref __result);
        }

        if (__instance is Pale)
            __result = FormatPaleDescription(__result);
    }

    private static void RemoveRenderedKeyword(
        CardKeyword keyword,
        ref string description)
    {
        string keywordText = keyword.GetCardText().Trim();
        if (string.IsNullOrWhiteSpace(keywordText) ||
            !description.Contains(keywordText, StringComparison.Ordinal))
        {
            return;
        }

        description = string.Join(
            '\n',
            description
                .Split('\n')
                .Select(line => line.Replace(
                    keywordText,
                    string.Empty,
                    StringComparison.Ordinal).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string FormatPerformanceLine(int turns)
    {
        var line = new LocString("cards", "MGR_MOD_CARD_COMBAT_PERFORMANCE_BONUS");
        line.Add("Times", turns);
        return line.GetFormattedText();
    }

    private static void ReplacePerformanceLine(ref string description, string replacement)
    {
        // Native Retain, Ethereal and Innate can precede the printed rules.
        // Match the localized keyword so those lines survive the replacement.
        int keywordEnd = replacement.IndexOf("[/gold]", StringComparison.Ordinal);
        string keyword = replacement[..(keywordEnd + "[/gold]".Length)];
        string[] lines = description.Split('\n');
        int index = Array.FindIndex(
            lines, line => line.TrimStart().StartsWith(keyword, StringComparison.Ordinal));
        if (index < 0)
            return;

        lines[index] = replacement;
        description = string.Join('\n', lines);
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
        if (!CompactTerminalKeywords.Any(card.Keywords.Contains))
            return;

        var keywordTexts = CompactTerminalKeywords
            .Where(card.Keywords.Contains)
            .Select(keyword => keyword.GetCardText().Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToHashSet(StringComparer.Ordinal);
        if (keywordTexts.Count == 0)
            return;

        List<string> lines = description.Split('\n').ToList();
        var matchedLines = lines
            .Select((line, index) => (Text: line.Trim(), Index: index))
            .Where(item => keywordTexts.Contains(item.Text))
            .ToList();
        if (matchedLines.Count == 0)
            return;

        string compactLine = string.Join(
            " ",
            matchedLines.OrderBy(item => item.Index).Select(item => item.Text));
        foreach (int index in matchedLines.Select(item => item.Index).OrderDescending())
            lines.RemoveAt(index);

        // Retain/Ethereal/Exhaust are terminal presentation keywords.
        // Tower 2 may place a lone keyword before the rules text while an
        // upgraded card with two keywords is compacted elsewhere. Always move
        // both the one-keyword and multi-keyword forms to the final line so an
        // upgrade cannot unexpectedly invert the card-description layout.
        lines.Add(compactLine);
        description = string.Join('\n', lines);
    }

    private static void MoveInnateToFirstLine(
        MgrCard card,
        ref string description)
    {
        MoveKeywordToFirstLine(card, CardKeyword.Innate, ref description);
    }

    private static void MoveKeywordToFirstLine(
        MgrCard card,
        CardKeyword keyword,
        ref string description)
    {
        if (!card.Keywords.Contains(keyword))
            return;

        string keywordText = keyword.GetCardText().Trim();
        if (string.IsNullOrWhiteSpace(keywordText))
            return;

        List<string> lines = description.Split('\n').ToList();
        int keywordIndex = lines.FindIndex(line => line.Trim() == keywordText);
        if (keywordIndex < 0)
        {
            keywordIndex = lines.FindIndex(line =>
                line.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Contains(keywordText, StringComparer.Ordinal));
        }
        if (keywordIndex < 0)
            return;

        string remaining = lines[keywordIndex]
            .Replace(keywordText, string.Empty, StringComparison.Ordinal)
            .Trim();
        if (string.IsNullOrWhiteSpace(remaining))
            lines.RemoveAt(keywordIndex);
        else
            lines[keywordIndex] = remaining;
        lines.Insert(0, keywordText);
        description = string.Join('\n', lines);
    }

    private static void CompactPerformanceExhaustLine(ref string description)
    {
        string exhaustText = CardKeyword.Exhaust.GetCardText().Trim();
        if (string.IsNullOrWhiteSpace(exhaustText))
            return;

        List<string> lines = description.Split('\n').ToList();
        int exhaustIndex = lines.FindIndex(line => line.Trim() == exhaustText);
        if (lines.Count == 0 || exhaustIndex < 0)
            return;

        lines.RemoveAt(exhaustIndex);
        lines[0] = $"{lines[0].Trim()} {exhaustText}";
        description = string.Join('\n', lines);
    }

    private static void CompactLightSongIdentityLine(ref string description)
    {
        string exhaustText = CardKeyword.Exhaust.GetCardText().Trim();
        if (string.IsNullOrWhiteSpace(exhaustText))
            return;

        List<string> lines = description.Split('\n').ToList();
        int exhaustIndex = lines.FindIndex(line => line.Trim() == exhaustText);
        if (lines.Count == 0 || exhaustIndex < 0)
            return;

        lines.RemoveAt(exhaustIndex);
        lines[0] = $"{lines[0].Trim()} {exhaustText}";
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

    private static void FormatStarryNoteText(ref string description)
    {
        var title = new LocString(
            "card_keywords",
            "MGR_MOD_KEYWORD_STARRY_NOTE.title");
        string starryNote = title.GetFormattedText();
        if (string.IsNullOrWhiteSpace(starryNote) ||
            !description.Contains(starryNote, StringComparison.Ordinal))
        {
            return;
        }

        // Normalize every localized Starry Note mention to the same purple,
        // floating presentation as Starry. A placeholder prevents the bare-title
        // replacement from nesting a second effect inside existing color tags.
        const string placeholder = "\uE000MGR_STARRY_NOTE\uE001";
        string styled = $"[sine][color=#b96cff]{starryNote}[/color][/sine]";
        description = description
            .Replace(styled, placeholder, StringComparison.Ordinal)
            .Replace(
                $"[color=#b96cff]{starryNote}[/color]",
                placeholder,
                StringComparison.Ordinal)
            .Replace(
                $"[gold]{starryNote}[/gold]",
                placeholder,
                StringComparison.Ordinal)
            .Replace(
                $"[sine]{starryNote}[/sine]",
                placeholder,
                StringComparison.Ordinal)
            .Replace(starryNote, placeholder, StringComparison.Ordinal)
            .Replace(placeholder, styled, StringComparison.Ordinal);
    }
}
