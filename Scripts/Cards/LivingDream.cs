using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "dreamlike_life")]
public sealed class LivingDream : MgrCard
{
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

            CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Hand,
                Owner);
            CardCmd.PreviewCardPileAdd(result);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
