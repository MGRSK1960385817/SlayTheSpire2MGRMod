using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "hyper_speed")]
public sealed class HyperSpeed : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        new CardsVar(3),
        new IntVar("Debt", 1m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public HyperSpeed() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await PowerCmd.Apply<HyperSpeedDebtPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Debt"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
