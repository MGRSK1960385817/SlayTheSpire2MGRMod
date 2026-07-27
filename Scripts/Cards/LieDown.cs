using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "lie_down")]
public sealed class LieDown : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(0)];

    public LieDown() : base(0, CardType.Status, CardRarity.Token, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        DynamicVars.Cards.IntValue > 0
            ? CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner)
            : Task.CompletedTask;

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
