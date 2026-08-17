using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "east_of_timeline")]
public sealed class EastOfTimeline : MgrCard
{
    private const int BaseNotes = 2;
    private int _currentNotes = BaseNotes;

    [SavedProperty]
    public int CurrentNotes
    {
        get => _currentNotes;
        private set
        {
            AssertMutable();
            _currentNotes = Math.Max(BaseNotes, value);
            DynamicVars["Notes"].BaseValue = _currentNotes;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", CurrentNotes),
        new IntVar("PermanentIncrease", 1m)
    ];

    // Explicitly Attack in the Tower-2 adaptation, per the design request.
    public EastOfTimeline() : base(1, CardType.Attack, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int notes = DynamicVars["Notes"].IntValue;
        for (int index = 0; index < notes; index++)
            await ChannelNote(choiceContext, NoteKind.Attack);
    }

    internal void IncreaseNotesPermanently()
    {
        decimal amount = DynamicVars["PermanentIncrease"].BaseValue;
        HashSet<CardModel> targets = [this];
        CardModel? deckVersion = DeckVersion;
        if (deckVersion is not null)
            targets.Add(deckVersion);

        foreach (PileType pile in new[]
                 {
                     PileType.Hand,
                     PileType.Draw,
                     PileType.Discard,
                     PileType.Exhaust,
                     PileType.Play
                 })
        {
            foreach (CardModel card in pile.GetPile(Owner).Cards)
            {
                if (ReferenceEquals(card, this) ||
                    (deckVersion is not null && ReferenceEquals(card.DeckVersion, deckVersion)))
                {
                    targets.Add(card);
                }
            }
        }

        int integerAmount = decimal.ToInt32(amount);
        foreach (CardModel target in targets)
        {
            if (target is EastOfTimeline timeline)
                timeline.CurrentNotes += integerAmount;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PermanentIncrease"].UpgradeValueBy(1m);
    }
}
