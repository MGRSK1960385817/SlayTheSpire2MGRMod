using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards.Choices;

[RegisterCard(typeof(TokenCardPool), StableEntryStem = "flawed_girl_1")]
public sealed class FlawedGirl1 : MgrCard, INoteSlotChoice
{
    public FlawedGirl1() : base(
        0,
        CardType.Skill,
        CardRarity.Token,
        TargetType.None,
        showInCardLibrary: false)
    {
    }

    public Task Apply(PlayerChoiceContext choiceContext, CardModel sourceCard) =>
        MgrNoteSystem.ChangeSlotCapacity(choiceContext, sourceCard.Owner, 2);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Apply(choiceContext, this);

    protected override void OnUpgrade()
    {
    }
}
