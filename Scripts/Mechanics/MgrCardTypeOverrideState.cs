using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace MGRMod.Mechanics;

/// <summary>
/// Replaces the real card type on combat instances made by A Tale of Mine.
/// CardModel exposes Type as a getter-only property, but its value lives in an
/// instance backing field. Writing that field makes the frame, type plaque,
/// play routing and all game rules observe the same authoritative type.
/// The weak marker additionally tells MGR note resolution to ignore special
/// non-Starry NoteOverride values after a card has been converted. A Starry
/// card keeps its intrinsic Starry Note identity even after its type changes.
/// </summary>
public static class MgrCardTypeOverrideState
{
    private static readonly FieldInfo TypeBackingField =
        typeof(CardModel).GetField(
            "<Type>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new MissingFieldException(
            typeof(CardModel).FullName,
            "<Type>k__BackingField");

    private sealed class TypeHolder(CardType type)
    {
        public CardType Type { get; } = type;
    }

    private static readonly ConditionalWeakTable<CardModel, TypeHolder> Overrides = new();

    public static void Set(CardModel card, CardType type)
    {
        ArgumentNullException.ThrowIfNull(card);

        // FieldInfo.SetValue supports readonly instance fields. This changes
        // only the mutable combat clone; canonical and run-deck models remain
        // untouched.
        TypeBackingField.SetValue(card, type);

        Overrides.Remove(card);
        Overrides.Add(card, new TypeHolder(type));

        if (card.Type != type)
        {
            throw new InvalidOperationException(
                $"Failed to change card {card.Id.Entry} type to {type}.");
        }
    }

    public static bool TryGet(CardModel card, out CardType type)
    {
        if (Overrides.TryGetValue(card, out TypeHolder? holder))
        {
            type = holder.Type;
            return true;
        }

        type = default;
        return false;
    }

    public static void Copy(CardModel source, CardModel destination)
    {
        if (TryGet(source, out CardType type))
            Set(destination, type);
    }
}
