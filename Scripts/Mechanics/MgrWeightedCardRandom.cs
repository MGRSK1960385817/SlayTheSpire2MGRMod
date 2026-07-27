using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Shared rarity-compensated random selection for MGR effects.
/// Common and all other rarities have weight 1, Uncommon has weight 1.5,
/// and Rare has weight 2. Integer weights are doubled to preserve the ratio.
/// </summary>
public static class MgrWeightedCardRandom
{
    public static CardModel? PickOne(
        IReadOnlyList<CardModel> candidates,
        Rng rng)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rng);
        if (candidates.Count == 0)
            return null;

        int totalWeight = candidates.Sum(GetRarityWeight);
        int roll = rng.NextInt(0, totalWeight);
        foreach (CardModel candidate in candidates)
        {
            roll -= GetRarityWeight(candidate);
            if (roll < 0)
                return candidate;
        }

        // Defensive fallback for an RNG implementation with unexpected bounds.
        return candidates[^1];
    }

    public static IReadOnlyList<CardModel> CreateDistinctForCombat(
        Player player,
        IEnumerable<CardModel> canonicalCandidates,
        int count,
        Rng rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(canonicalCandidates);
        ArgumentNullException.ThrowIfNull(rng);
        if (count <= 0 || player.Creature.CombatState is null)
            return [];

        List<CardModel> available = CardFactory
            .FilterForCombat(canonicalCandidates)
            .ToList();
        var result = new List<CardModel>(Math.Min(count, available.Count));

        while (result.Count < count && available.Count > 0)
        {
            CardModel? canonical = PickOne(available, rng);
            if (canonical is null)
                break;

            available.Remove(canonical);
            result.Add(player.Creature.CombatState.CreateCard(canonical, player));
        }

        return result;
    }

    private static int GetRarityWeight(CardModel card) => card.Rarity switch
    {
        CardRarity.Uncommon => 3,
        CardRarity.Rare => 4,
        _ => 2
    };
}
