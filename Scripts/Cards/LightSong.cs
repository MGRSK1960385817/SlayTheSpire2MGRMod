using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "light_song")]
public sealed class LightSong : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public LightSong() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> drawPileSnapshot = PileType.Draw.GetPile(Owner).Cards.ToList();

        foreach (NoteKind kind in Enum.GetValues<NoteKind>())
        {
            List<CardModel> candidates = drawPileSnapshot
                .Where(card => CardNoteResolver.Resolve(card) == kind)
                .ToList();
            CardModel? matchingCard = MgrWeightedCardRandom.PickOne(
                candidates,
                Owner.RunState.Rng.CombatCardGeneration);
            if (matchingCard is null)
                continue;

            // Remove it from the snapshot so later categories can never select
            // the same model if mapping rules gain aliases in the future.
            drawPileSnapshot.Remove(matchingCard);
            await CardPileCmd.Add(matchingCard, PileType.Hand);
            await Cmd.Wait(0.1f);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
