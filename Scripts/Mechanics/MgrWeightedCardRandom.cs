using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MGRMod.Cards;

namespace MGRMod.Mechanics;

public enum MgrCardWeightProfile
{
    Uniform,
    Standard,
    GentleCompensation,
    ElectricAngel
}

/// <summary>
/// Shared random selection for MGR effects. Uniform gives every candidate the
/// same chance. Standard preserves Light Song's Common 1, Uncommon 1.5 and
/// Rare 2 weighting; GentleCompensation is retained for explicitly weighted
/// effects only.
/// </summary>
public static class MgrWeightedCardRandom
{
    public static CardModel? PickOne(
        IReadOnlyList<CardModel> candidates,
        Rng rng,
        MgrCardWeightProfile profile = MgrCardWeightProfile.Standard)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rng);
        if (candidates.Count == 0)
            return null;

        int totalWeight = candidates.Sum(card => GetWeight(card, profile));
        int roll = rng.NextInt(0, totalWeight);
        foreach (CardModel candidate in candidates)
        {
            roll -= GetWeight(candidate, profile);
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
        Rng rng,
        MgrCardWeightProfile profile = MgrCardWeightProfile.Standard)
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
            CardModel? canonical = PickOne(available, rng, profile);
            if (canonical is null)
                break;

            available.Remove(canonical);
            result.Add(player.Creature.CombatState.CreateCard(canonical, player));
        }

        return result;
    }

    private static int GetWeight(
        CardModel card,
        MgrCardWeightProfile profile)
    {
        if (profile == MgrCardWeightProfile.Uniform)
            return 1;

        if (profile == MgrCardWeightProfile.ElectricAngel)
        {
            // Relative per-card weights: Common 1.0, Uncommon 0.8, Rare 0.6.
            return card.Rarity switch
            {
                CardRarity.Rare => 3,
                CardRarity.Uncommon => 4,
                _ => 5
            };
        }

        if (profile == MgrCardWeightProfile.GentleCompensation)
        {
            if (card is Regulus)
                return 2;

            return card.Rarity switch
            {
                CardRarity.Uncommon => 5,
                CardRarity.Rare => 6,
                _ => 4
            };
        }

        return card.Rarity switch
        {
            CardRarity.Uncommon => 3,
            CardRarity.Rare => 4,
            _ => 2
        };
    }
}
