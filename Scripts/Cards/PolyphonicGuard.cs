using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "polyphonic_guard")]
public sealed class PolyphonicGuard : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
        new CardsVar(1)
    ];

    public PolyphonicGuard() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int kinds = NoteState.Phrase.Notes
            .Select(note => note.Kind)
            .Distinct()
            .Count();
        if (kinds <= 0)
            return;

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block.BaseValue * kinds,
            ValueProp.Move,
            cardPlay);
        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue * kinds,
            Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}
