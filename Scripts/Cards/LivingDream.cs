using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "living_dream")]
public sealed class LivingDream : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public LivingDream() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IReadOnlyList<MgrNote> notes = MgrNoteSystem.RemoveAllNotes(Owner);
        foreach (MgrNote note in notes)
        {
            CardModel? card = MgrNoteCardFactory.CreateRandomCard(Owner, note.Kind, IsUpgraded);
            if (card is null)
                continue;

            // Match Blade Dance's fast hand-generation presentation: let the
            // native hand add animate the card and avoid the separate 1.2s
            // centre-screen pile preview.
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Hand,
                Owner);
            await Cmd.Wait(0.1f);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
