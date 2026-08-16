using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace MGRMod.Mechanics;

/// <summary>
/// Reverse mapping for effects that turn an MGR note back into a random card.
/// Ordinary and MGR-special notes use the character pool; Status and Curse
/// notes use Tower 2's dedicated pools.
/// </summary>
public static class MgrNoteCardFactory
{
    public static CardModel? CreateRandomCard(
        Player player,
        NoteKind kind,
        bool upgraded)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.CombatState is not { } combatState)
            return null;

        CardModel[] candidates = GetCandidates(player, kind)
            .Where(card => card.CanBeGeneratedInCombat)
            .Where(card => ResolvesTo(card, kind))
            .ToArray();
        if (candidates.Length == 0)
            return null;

        CardModel? canonical = kind == NoteKind.Curse
            ? MgrCurseUtils.PickRandomCurseCanonical(
                candidates,
                player.RunState.Rng.CombatCardGeneration)
            : MgrWeightedCardRandom.PickOne(
                candidates,
                player.RunState.Rng.CombatCardGeneration,
                MgrCardWeightProfile.Uniform);
        if (canonical is null)
            return null;

        CardModel card = combatState.CreateCard(canonical, player);
        if (upgraded)
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        return card;
    }

    private static IEnumerable<CardModel> GetCandidates(Player player, NoteKind kind) =>
        kind switch
        {
            NoteKind.Status => ModelDb.CardPool<StatusCardPool>().AllCards,
            NoteKind.Curse => ModelDb.CardPool<CurseCardPool>().AllCards,
            _ => CardFactory.FilterForCombat(
                player.Character.CardPool.GetUnlockedCards(
                    player.UnlockState,
                    player.RunState.CardMultiplayerConstraint))
        };

    private static bool ResolvesTo(CardModel card, NoteKind expected)
    {
        try
        {
            return CardNoteResolver.Resolve(card) == expected;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
