using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "galaxy_lamp")]
public sealed class GalaxyLamp : MgrCard
{
    public override bool IsStarryCard => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public GalaxyLamp() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        NoteKind[] noteKinds = NoteState.Phrase.Notes
            .Select(static note => note.Kind)
            .ToArray();
        await MgrRegentStructureVfx.PlayGalaxyLampConversion(
            this,
            Owner.Creature,
            noteKinds);
        int removedCount = MgrNoteSystem.RemoveAllNotes(Owner).Count;
        for (int index = 0; index < removedCount; index++)
            await ChannelNote(choiceContext, NoteKind.Starry);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
