using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Explanatory hover boxes for MGR rules-language phrases that should help the
/// player but are not gameplay keywords. Keeping them separate from registered
/// card keywords prevents them from participating in keyword parsing or styling.
/// </summary>
internal static class MgrHoverTips
{
    private const string SupplementalIdPrefix = "SLAY_THE_SPIRE2_MGR_MOD_SUPPLEMENTAL_HOVER_TIP:";
    private const string CardsInCombatKey =
        "SLAY_THE_SPIRE2_MGR_MOD_HOVER_TIP_CARDS_IN_COMBAT";
    private const string BaseDamageKey =
        "SLAY_THE_SPIRE2_MGR_MOD_HOVER_TIP_BASE_DAMAGE";
    private const string TransformIntoNoteKey =
        "SLAY_THE_SPIRE2_MGR_MOD_HOVER_TIP_TRANSFORM_INTO_NOTE";

    public static IHoverTip CardsInCombat() => new HoverTip(
        new LocString("static_hover_tips", $"{CardsInCombatKey}.title"),
        new LocString("static_hover_tips", $"{CardsInCombatKey}.description"))
    {
        Id = $"{SupplementalIdPrefix}{CardsInCombatKey}"
    };

    public static IHoverTip BaseDamage() => new HoverTip(
        new LocString("static_hover_tips", $"{BaseDamageKey}.title"),
        new LocString("static_hover_tips", $"{BaseDamageKey}.description"))
    {
        Id = $"{SupplementalIdPrefix}{BaseDamageKey}"
    };

    public static IHoverTip TransformIntoNote() => new HoverTip(
        new LocString("static_hover_tips", $"{TransformIntoNoteKey}.title"),
        new LocString("static_hover_tips", $"{TransformIntoNoteKey}.description"))
    {
        Id = $"{SupplementalIdPrefix}{TransformIntoNoteKey}"
    };

    internal static bool IsSupplemental(IHoverTip hoverTip) =>
        hoverTip.Id.StartsWith(SupplementalIdPrefix, StringComparison.Ordinal);
}
