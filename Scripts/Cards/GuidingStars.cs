using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "guiding_stars")]
public sealed class GuidingStars : MgrCard
{
    public override bool IsStarryCard => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move)
    ];

    public GuidingStars() : base(
        2,
        CardType.Attack,
        CardRarity.Uncommon,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await MgrAttackVfx.PlaySmallMagicMissile(
            this,
            cardPlay.Target,
            MgrAttackVfx.StarPurple);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitVfxNode(target => MgrAttackVfx.CreateStarryImpact(
                target,
                MgrAttackVfx.StarPurple,
                0.9f))
            .WithHitFx(null, null, "blunt_attack.mp3")
            .Execute(choiceContext);

        if (Owner.Creature.CombatState is not { } combatState)
            return;

        CardModel[] candidates = CardFactory
            .FilterForCombat(Owner.Character.CardPool.GetUnlockedCards(
                Owner.UnlockState,
                Owner.RunState.CardMultiplayerConstraint))
            .OfType<MgrCard>()
            .Where(card => card.IsStarryCard && card.CanBeGeneratedInCombat)
            .Cast<CardModel>()
            .ToArray();
        CardModel? canonical = PickWeightedStarryCard(candidates);
        if (canonical is null)
            return;

        CardModel generated = combatState.CreateCard(canonical, Owner);
        await MgrPerformanceSystem.EnqueueGeneratedCard(
            Owner,
            generated,
            previewBeforeEnqueue: true);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }

    private CardModel? PickWeightedStarryCard(IReadOnlyList<CardModel> candidates)
    {
        if (candidates.Count == 0)
            return null;

        // Normal Starry cards have weight 3. Satellite Girl and Regulus use
        // weights 2 and 1 respectively, i.e. 2/3 and 1/3 of the normal rate.
        int totalWeight = candidates.Sum(GetStarryWeight);
        int roll = Owner.RunState.Rng.CombatCardGeneration.NextInt(0, totalWeight);
        foreach (CardModel candidate in candidates)
        {
            roll -= GetStarryWeight(candidate);
            if (roll < 0)
                return candidate;
        }

        return candidates[^1];
    }

    private static int GetStarryWeight(CardModel card) => card switch
    {
        SatelliteGirl => 2,
        Regulus => 1,
        _ => 3
    };
}
