using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// A native calculated-damage variable whose final, fully modified preview can
/// be doubled by a card-local condition. The multiplier is applied after the
/// base calculated var has processed Strength, enchantments and target effects,
/// so the printed number and the damage executed by AttackCommand stay aligned.
/// </summary>
public sealed class MgrConditionalCalculatedDamageVar(
    ValueProp props,
    Func<CardModel, bool> shouldDouble)
    : CalculatedDamageVar(props)
{
    public override void UpdateCardPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature? target,
        bool runGlobalHooks)
    {
        base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        if (shouldDouble(card))
            PreviewValue *= 2m;
    }
}
