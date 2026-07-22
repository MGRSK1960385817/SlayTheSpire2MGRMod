using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "cubic_prism")]
public sealed class CubicPrism : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<CubicPrismPower>(6m)
    ];

    public CubicPrism() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<CubicPrismPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["CubicPrismPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["CubicPrismPower"].UpgradeValueBy(2m);
}
