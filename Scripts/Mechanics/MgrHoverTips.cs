using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace MGRMod.Mechanics;

/// <summary>
/// Explanatory hover boxes for MGR rules-language phrases that should help the
/// player but are not gameplay keywords. Keeping them separate from registered
/// card keywords prevents them from participating in keyword parsing or styling.
/// </summary>
internal static class MgrHoverTips
{
    private const string SupplementalKeyPrefix = "MGR_MOD_HOVER_TIP_";
    private const string CardsInCombatKey =
        "MGR_MOD_HOVER_TIP_CARDS_IN_COMBAT";
    private const string BaseDamageKey =
        "MGR_MOD_HOVER_TIP_BASE_DAMAGE";
    private const string TransformIntoNoteKey =
        "MGR_MOD_HOVER_TIP_TRANSFORM_INTO_NOTE";

    public static IHoverTip CardsInCombat() => new HoverTip(
        new LocString("static_hover_tips", $"{CardsInCombatKey}.title"),
        new LocString("static_hover_tips", $"{CardsInCombatKey}.description"));

    public static IHoverTip BaseDamage() => new HoverTip(
        new LocString("static_hover_tips", $"{BaseDamageKey}.title"),
        new LocString("static_hover_tips", $"{BaseDamageKey}.description"));

    public static IHoverTip TransformIntoNote() => new HoverTip(
        new LocString("static_hover_tips", $"{TransformIntoNoteKey}.title"),
        new LocString("static_hover_tips", $"{TransformIntoNoteKey}.description"));

    internal static bool IsSupplemental(IHoverTip hoverTip) =>
        hoverTip.Id.Contains(SupplementalKeyPrefix, StringComparison.Ordinal);
}
