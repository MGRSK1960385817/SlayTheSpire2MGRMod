using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "starlit_encore")]
public sealed class StarlitEncore : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new IntVar("Performance", 2m)
    ];

    public override bool IsStarryCard => true;

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public StarlitEncore() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(1m);
    }
}
