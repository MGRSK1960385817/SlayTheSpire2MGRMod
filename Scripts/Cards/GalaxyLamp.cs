using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "galaxy_lamp")]
public sealed class GalaxyLamp : MgrCard
{
    public override bool IsStarryCard => true;

    public GalaxyLamp() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        int removedCount = MgrNoteSystem.RemoveAllNotes(Owner).Count;
        for (int index = 0; index < removedCount; index++)
            await ChannelNote(choiceContext, NoteKind.Starry);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
