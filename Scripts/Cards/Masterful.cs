using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "masterful")]
public sealed class Masterful : MgrCard
{
    private static readonly NoteKind[] SelectionOrder =
    [
        NoteKind.Attack,
        NoteKind.Skill,
        NoteKind.Power,
        NoteKind.Status,
        NoteKind.Curse,
        NoteKind.Quest,
        NoteKind.Starry
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public Masterful() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> drawPileSnapshot = PileType.Draw.GetPile(Owner).Cards.ToList();

        foreach (NoteKind kind in SelectionOrder)
        {
            CardModel? matchingCard = drawPileSnapshot.FirstOrDefault(
                card => CardNoteResolver.Resolve(card) == kind);
            if (matchingCard is null)
                continue;

            // Remove it from the snapshot so later categories can never select
            // the same model if mapping rules gain aliases in the future.
            drawPileSnapshot.Remove(matchingCard);
            await CardPileCmd.Add(matchingCard, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
