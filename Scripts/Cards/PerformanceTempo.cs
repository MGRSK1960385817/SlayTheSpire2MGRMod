using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "performance_tempo")]
public sealed class PerformanceTempo : MgrCard
{
    public override bool GainsBlock => true;

    protected override MgrKeywordKind KeywordKinds =>
        MgrKeywordKind.Performance;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new IntVar("Discount", 1m)
    ];

    public PerformanceTempo() : base(
        2,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<NextPerformanceDiscountPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Discount"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() =>
        DynamicVars.Block.UpgradeValueBy(4m);
}
