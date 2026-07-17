using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "rehearsal")]
public sealed class Rehearsal : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public Rehearsal() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IEnumerable<CardModel> drawnCards = await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);

        foreach (CardModel drawnCard in drawnCards)
            await ChannelNote(choiceContext, CardNoteResolver.Resolve(drawnCard));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
