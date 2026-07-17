using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "climax")]
public sealed class Climax : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FortePower>(2m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public Climax() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal amount = DynamicVars["FortePower"].BaseValue;
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
        await PowerCmd.Apply<TemporaryFortePower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
