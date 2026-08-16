using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "spring_storm")]
public sealed class SpringStorm : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FortePower>(1m)
    ];

    public SpringStorm() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
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
        MgrSpringStormVfx.Show(Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
