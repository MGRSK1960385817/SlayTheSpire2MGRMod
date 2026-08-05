using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "chorus")]
public sealed class Chorus : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new IntVar("CardsPerBatch", 5m),
        new IntVar("Notes", 3m),
        new CalculatedVar("CalculatedNotes").WithMultiplier(
            (card, _) =>
            {
                if (card.CombatState is null)
                    return 0m;

                int cardsPerBatch = card.DynamicVars["CardsPerBatch"].IntValue;
                int notesPerBatch = card.DynamicVars["Notes"].IntValue;
                int drawPileCards = PileType.Draw.GetPile(card.Owner).Cards.Count;
                return drawPileCards / cardsPerBatch * notesPerBatch;
            })
    ];

    public Chorus() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int notesToGenerate = (int)((CalculatedVar)DynamicVars["CalculatedNotes"])
            .Calculate(cardPlay.Target);
        for (int index = 0; index < notesToGenerate; index++)
            await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(1m);
    }
}
