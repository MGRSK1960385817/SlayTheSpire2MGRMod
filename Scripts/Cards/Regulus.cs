using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "regulus")]
public sealed class Regulus : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new IntVar("Hits", 14m),
        new IntVar("CostReduction", 3m)
    ];

    public override bool IsStarryCard => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Retain]);

    public Regulus() : base(14, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);
    }

    public override Task AfterCardDiscarded(
        PlayerChoiceContext choiceContext,
        CardModel card) => ReturnAfterLeavingPile(card);

    public override Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal) => ReturnAfterLeavingPile(card);

    private async Task ReturnAfterLeavingPile(CardModel card)
    {
        if (!ReferenceEquals(card, this))
            return;

        EnergyCost.AddThisCombat(-DynamicVars["CostReduction"].IntValue);
        await CardPileCmd.Add(this, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CostReduction"].UpgradeValueBy(1m);
    }
}
