using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "refrain_form")]
public sealed class RefrainForm : MgrCard
{
    public RefrainForm() : base(
        3,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        NoteKind[] snapshot = MgrCombatStateStore.For(Owner)
            .Phrase
            .Notes
            .Select(note => note.Kind)
            .ToArray();

        await PowerCmd.Apply<RefrainFormPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        Owner.Creature.GetPower<RefrainFormPower>()?.Record(snapshot);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
