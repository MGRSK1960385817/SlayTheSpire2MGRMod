using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "note_magic")]
public sealed class NoteMagic : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        new IntVar("Copies", 1m)
    ];

    public NoteMagic() : base(
        1,
        CardType.Skill,
        CardRarity.Common,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await MgrNoteSystem.CopyRightmostNotes(
            choiceContext,
            Owner,
            DynamicVars["Copies"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Copies"].UpgradeValueBy(1m);
        DynamicVars.Block.UpgradeValueBy(1m);
    }
        
}
