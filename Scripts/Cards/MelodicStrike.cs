using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "melodic_strike")]
public sealed class MelodicStrike : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        new IntVar("Performance", 1m),
        new PowerVar<FortePower>(1m)
    ];

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    public MelodicStrike() : base(
        1,
        CardType.Attack,
        CardRarity.Common,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(VfxCmd.slashPath)
            .Execute(choiceContext);

        decimal forteAmount = DynamicVars["FortePower"].BaseValue;
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner.Creature,
            forteAmount,
            Owner.Creature,
            this);
        await PowerCmd.Apply<TemporaryFortePower>(
            choiceContext,
            Owner.Creature,
            forteAmount,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
