using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "opening_duet")]
[RegisterCharacterStarterCard(typeof(MgrCharacter), Order = 30)]
public sealed class OpeningDuet : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 3m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public OpeningDuet() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ChannelNote(choiceContext, NoteKind.Attack);
        await ChannelNote(choiceContext, NoteKind.Skill);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(2m);
    }
}
