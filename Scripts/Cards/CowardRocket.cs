using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "coward_rocket")]
public sealed class CowardRocket : MgrCard
{
    public override bool GainsBlock => true;
    public override bool HasTurnEndInHandEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    public CowardRocket() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    protected override Task OnTurnEndInHand(PlayerChoiceContext choiceContext) =>
        MgrPerformanceSystem.EnqueueCardFromHand(Owner, this, 1);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}
