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

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public GalaxyLamp() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MgrNoteSystem.ReplaceAllNotes(Owner, NoteKind.Starry);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
