using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Shared Tower-2 implementation for curse generation and pile inspection.
/// Keeping this here prevents curse cards from each inventing their own pool logic.
/// </summary>
public static class MgrCurseUtils
{
    private static readonly PileType[] CountedCombatPiles =
    [
        PileType.Hand,
        PileType.Draw,
        PileType.Discard,
        PileType.Exhaust
    ];

    public static CardModel CreateRandomCurse(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.CombatState is not { } combatState)
            throw new InvalidOperationException("A random combat curse requires an active combat state.");

        CardModel[] candidates = ModelDb.CardPool<CurseCardPool>()
            .AllCards
            .Where(card => card.Type == CardType.Curse && card.CanBeGeneratedInCombat)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("The Tower-2 curse pool contains no generatable curses.");

        CardModel canonical = player.RunState.Rng.CombatCardGeneration.NextItem(candidates)
            ?? throw new InvalidOperationException("Tower-2 returned no random curse candidate.");
        return combatState.CreateCard(canonical, player);
    }

    public static async Task<CardModel> AddRandomCurseToCombat(
        Player player,
        PileType pileType,
        CardPilePosition position = CardPilePosition.Random)
    {
        CardModel curse = CreateRandomCurse(player);
        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            curse,
            pileType,
            player,
            position);
        CardCmd.PreviewCardPileAdd(result);
        return curse;
    }

    public static int CountCurses(Player player) =>
        CountedCombatPiles.Sum(pile =>
            pile.GetPile(player).Cards.Count(card => card.Type == CardType.Curse));

    public static CardModel[] SnapshotCursesAndStatuses(Player player, params PileType[] piles) =>
        piles
            .SelectMany(pile => pile.GetPile(player).Cards)
            .Where(card => card.Type is CardType.Curse or CardType.Status)
            .ToArray();
}
