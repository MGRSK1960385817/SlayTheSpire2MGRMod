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
    // These three vanilla curses are deliberately excluded from every random
    // curse effect in MGR: Enthralled (执迷), Debt (债务), Bad Luck (霉运).
    // Type names are used here so the rule remains centralized without tying
    // this utility to the concrete vanilla card namespaces.
    private static readonly HashSet<string> ExcludedRandomCurseTypeNames =
    [
        "Enthralled",
        "Debt",
        "BadLuck"
    ];

    private static readonly PileType[] CountedCombatPiles =
    [
        PileType.Hand,
        PileType.Draw,
        PileType.Discard,
        PileType.Exhaust
    ];

    public static bool IsExcludedRandomCurse(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Type == CardType.Curse &&
            ExcludedRandomCurseTypeNames.Contains(card.GetType().Name);
    }

    public static CardModel CreateRandomCurse(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.CombatState is not { } combatState)
            throw new InvalidOperationException("A random combat curse requires an active combat state.");

        CardModel[] candidates = ModelDb.CardPool<CurseCardPool>()
            .AllCards
            .Where(card =>
                card.Type == CardType.Curse &&
                card.CanBeGeneratedInCombat &&
                !IsExcludedRandomCurse(card))
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
        CardPilePosition position = CardPilePosition.Random,
        float pilePreviewDuration = 2f,
        float pilePreviewWait = 0.8f)
    {
        CardModel curse = CreateRandomCurse(player);
        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            curse,
            pileType,
            player,
            position);

        if (pileType == PileType.Hand)
        {
            // Blade Dance-style generated hand card: use only the native hand
            // fly-in and its short cadence, without a centre-screen preview.
            await Cmd.Wait(0.1f);
        }
        else
        {
            // Gunk Up-style generated pile card. MGR curse effects can create
            // several cards in sequence, so keep each preview on screen longer
            // than the vanilla default cadence to make its identity readable.
            CardCmd.PreviewCardPileAdd(
                result,
                Math.Max(0.05f, pilePreviewDuration));
            await Cmd.Wait(Math.Max(0f, pilePreviewWait));
        }

        return curse;
    }

    public static int CountCurses(Player player) =>
        CountedCombatPiles.Sum(pile =>
            pile.GetPile(player).Cards.Count(card => card.Type == CardType.Curse)) +
        MgrPerformanceSystem.GetQueuedCards(player)
            .Count(card => card.Type == CardType.Curse);

    public static CardModel[] SnapshotCursesAndStatuses(
        Player player,
        bool includePerformanceQueue,
        params PileType[] piles)
    {
        IEnumerable<CardModel> cards = piles
            .SelectMany(pile => pile.GetPile(player).Cards)
            .Where(card => card.Type is CardType.Curse or CardType.Status);

        if (includePerformanceQueue)
        {
            cards = cards.Concat(
                MgrPerformanceSystem.GetQueuedCards(player)
                    .Where(card => card.Type is CardType.Curse or CardType.Status));
        }

        return cards.Distinct().ToArray();
    }

    public static CardModel[] SnapshotCursesAndStatuses(Player player, params PileType[] piles) =>
        SnapshotCursesAndStatuses(player, includePerformanceQueue: false, piles);
}
