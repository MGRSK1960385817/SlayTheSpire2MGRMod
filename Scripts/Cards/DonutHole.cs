using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "donut_hole")]
public sealed class DonutHole : MgrCard
{
    public override bool GainsBlock => true;

    protected override MgrKeywordKind KeywordKinds =>
        MgrKeywordKind.Performance;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(14m, ValueProp.Move)
    ];

    public DonutHole() : base(
        0,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) ||
            !MgrPerformanceStateStore.TryGet(Owner, out MgrPerformanceState state) ||
            state.Entries.Count == 0)
        {
            return false;
        }

        modifiedCost = originalCost + state.Entries.Count;
        return true;
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
        => await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() =>
        DynamicVars.Block.UpgradeValueBy(4m);
}
