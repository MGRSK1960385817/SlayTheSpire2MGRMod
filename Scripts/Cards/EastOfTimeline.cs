using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "east_of_timeline")]
public sealed class EastOfTimeline : MgrCard
{
    private int _playsThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 2m),
        new IntVar("Uses", 2m)
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

        _playsThisCombat++;
        bool isFirstUse = _playsThisCombat == 1;
        bool isFinalUse = _playsThisCombat >= DynamicVars["Uses"].IntValue;
        if (isFirstUse || isFinalUse)
            IncreaseNotesPermanently(1m);
        if (isFinalUse)
            ExhaustOnNextPlay = true;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, this))
        {
            _playsThisCombat = 0;
            ExhaustOnNextPlay = false;
        }

        return Task.CompletedTask;
    }

    private void IncreaseNotesPermanently(decimal amount)
    {
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

        foreach (CardModel target in targets)
        {
            if (target.DynamicVars.TryGetValue("Notes", out var notesVar))
                notesVar.BaseValue += amount;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Uses"].UpgradeValueBy(1m);
    }
}
