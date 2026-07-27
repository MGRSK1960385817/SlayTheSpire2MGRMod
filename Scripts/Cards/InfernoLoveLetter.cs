using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "finale")]
public sealed class InfernoLoveLetter : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("NotesPerCard", 2m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    public InfernoLoveLetter() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel[] discarded = PileType.Hand.GetPile(Owner).Cards.ToArray();

        foreach (CardModel card in discarded)
            await CardCmd.Discard(choiceContext, card);

        int notes = discarded.Length * DynamicVars["NotesPerCard"].IntValue;
        for (int index = 0; index < notes; index++)
            await ChannelNote(choiceContext, NoteKind.Attack);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["NotesPerCard"].UpgradeValueBy(1m);
    }
}
