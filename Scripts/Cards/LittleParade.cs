using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "little_parade")]
[RegisterCharacterStarterCard(typeof(MgrCharacter), Order = 30)]
[RegisterArchaicToothTranscendence(typeof(Omnia))]
public sealed class LittleParade : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 4m),
        new IntVar("Notes", 1m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public LittleParade() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        for (int index = 0; index < DynamicVars["Notes"].IntValue; index++)
        {
            await ChannelNote(choiceContext, NoteKind.Attack);
            await ChannelNote(choiceContext, NoteKind.Skill);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(1m);
    }
}
