using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "meteor_aftermath")]
public sealed class MeteorAftermath : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        new EnergyVar("Debt", 2)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public MeteorAftermath() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await MgrRegentStructureVfx.PlayMeteorAftermath(
            this,
            Owner.Creature);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        int cardsToDraw = CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count;
        if (cardsToDraw > 0)
            await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner);
        await PowerCmd.Apply<HyperSpeedDebtPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Debt"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
