using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards.Choices;

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "bug_0")]
public sealed class BUG0 : MgrCard, INoteSlotChoice
{
    public BUG0() : base(
        0,
        CardType.Skill,
        CardRarity.Token,
        TargetType.None,
        showInCardLibrary: false)
    {
    }

    public Task Apply(PlayerChoiceContext choiceContext, CardModel sourceCard) =>
        MgrNoteSystem.ChangeSlotCapacity(choiceContext, sourceCard.Owner, -1);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Apply(choiceContext, this);

    protected override void OnUpgrade()
    {
    }
}
