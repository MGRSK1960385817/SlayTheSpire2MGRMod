using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Random;

namespace MGRMod.Mechanics;

/// <summary>
/// Shared Tower-2 implementation for curse generation and pile inspection.
/// Keeping this here prevents curse cards from each inventing their own pool logic.
/// </summary>
public static class MgrCurseUtils
{
    // Integer weights use 6 as a common denominator. Every ordinary curse has
    // weight 6; Bad Luck and Debt have half that weight, while Enthralled has
    // one third. Type names keep the shared utility independent of the vanilla
    // curse namespaces while still covering every MGR random-curse source.
    private const int OrdinaryCurseWeight = 6;

    private static readonly IReadOnlyDictionary<string, int>
        ReducedRandomCurseWeights = new Dictionary<string, int>
        {
            ["BadLuck"] = 3,
            ["Debt"] = 3,
            ["Enthralled"] = 2
        };

    private static readonly PileType[] CountedCombatPiles =
    [
        PileType.Hand,
        PileType.Draw,
        PileType.Discard,
        PileType.Exhaust
    ];

    public static int GetRandomCurseWeight(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ReducedRandomCurseWeights.TryGetValue(
            card.GetType().Name,
            out int weight)
            ? weight
            : OrdinaryCurseWeight;
    }

    public static CardModel? PickRandomCurseCanonical(
        IReadOnlyList<CardModel> candidates,
        Rng rng)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rng);
        if (candidates.Count == 0)
            return null;

        int totalWeight = candidates.Sum(GetRandomCurseWeight);
        int roll = rng.NextInt(0, totalWeight);
        foreach (CardModel candidate in candidates)
        {
            roll -= GetRandomCurseWeight(candidate);
            if (roll < 0)
                return candidate;
        }

        return candidates[^1];
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
                card.CanBeGeneratedInCombat)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("The Tower-2 curse pool contains no generatable curses.");

        CardModel canonical = PickRandomCurseCanonical(
                candidates,
                player.RunState.Rng.CombatCardGeneration)
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
