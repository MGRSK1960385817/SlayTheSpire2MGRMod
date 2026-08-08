using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "guiding_stars")]
public sealed class GuidingStars : MgrCard
{
    public override bool IsStarryCard => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move)
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
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
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
        CardModel? canonical = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
        if (canonical is null)
            return;

        CardModel generated = combatState.CreateCard(canonical, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
