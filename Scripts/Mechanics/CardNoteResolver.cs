using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Cards;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Converts any played or otherwise inspected card into its corresponding MGR note.
/// This resolver is intentionally independent of card-play hooks so discard and other
/// future mechanics can use the exact same mapping.
/// </summary>
public static class CardNoteResolver
{
    public static NoteKind Resolve(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is MgrCard { NoteOverride: { } noteOverride })
            return noteOverride;

        return card.Type switch
        {
            CardType.Attack => NoteKind.Attack,
            CardType.Skill => NoteKind.Skill,
            CardType.Power => NoteKind.Power,
            CardType.Status => NoteKind.Status,
            CardType.Curse => NoteKind.Curse,
            CardType.Quest => NoteKind.Quest,
            CardType.None => throw new ArgumentException(
                $"Card {card.Id} has CardType.None and cannot generate a note.", nameof(card)),
            _ => throw new ArgumentOutOfRangeException(nameof(card), card.Type, "Unknown card type.")
        };
    }
}
