using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "unison")]
public sealed class Unison : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 1m)
    ];

    public Unison() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<UnisonPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Notes"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
