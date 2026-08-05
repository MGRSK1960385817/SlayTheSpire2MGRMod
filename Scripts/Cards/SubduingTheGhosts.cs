using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "subduing_the_ghosts")]
public sealed class SubduingTheGhosts : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public SubduingTheGhosts() : base(
        1,
        CardType.Skill,
        CardRarity.Common,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        bool mayRepeat = true;
        while (mayRepeat)
        {
            CardModel[] drawn = (await CardPileCmd.Draw(
                choiceContext,
                DynamicVars.Cards.BaseValue,
                Owner)).ToArray();

            bool drewCurseOrStatus = drawn.Any(card =>
                card.Type is CardType.Curse or CardType.Status);
            if (!drewCurseOrStatus)
                break;

            // The base card grants only one extra draw. Its upgrade lets each
            // successful batch start another batch until the condition fails.
            mayRepeat = IsUpgraded;
            if (!mayRepeat)
            {
                await CardPileCmd.Draw(
                    choiceContext,
                    DynamicVars.Cards.BaseValue,
                    Owner);
            }
        }
    }

    protected override void OnUpgrade()
    {
    }
}
