using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "futariboshi")]
public sealed class Futariboshi : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public override bool IsStarryCard => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.AtLeastTwoNotes;

    public Futariboshi() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int removeCount = DynamicVars.Cards.IntValue;
        if (NoteState.Phrase.Notes.Count < removeCount)
            return;

        MgrNoteSystem.RemoveRightmostNotes(Owner, removeCount);
        await ChannelNote(choiceContext, NoteKind.Starry);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
