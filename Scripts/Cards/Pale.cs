using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(TokenCardPool), StableEntryStem = "pale")]
public sealed class Pale : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(0)];

    public Pale() : base(
        0,
        CardType.Status,
        CardRarity.Token,
        TargetType.Self,
        showInCardLibrary: false)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        DynamicVars.Cards.IntValue > 0
            ? CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner)
            : Task.CompletedTask;

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
