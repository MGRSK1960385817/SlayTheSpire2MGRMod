using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "ode_to_ultramarine")]
public sealed class OdeToUltramarine : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(2m, ValueProp.Move),
        new IntVar("Performance", 6m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public OdeToUltramarine() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsFirstInSeries)
        {
            MgrBlueCardVfx.SpawnUltramarineGuard(
                Owner.Creature,
                InitialPerformanceTurns);
        }

        return CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars["Performance"].UpgradeValueBy(1m);
    }
}
