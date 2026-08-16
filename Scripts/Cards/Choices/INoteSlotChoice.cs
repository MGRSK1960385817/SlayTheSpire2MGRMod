using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MGRMod.Cards.Choices;

public interface INoteSlotChoice
{
    Task Apply(PlayerChoiceContext choiceContext, CardModel sourceCard);
}
