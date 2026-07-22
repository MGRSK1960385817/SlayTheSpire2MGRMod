using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

/// <summary>
/// Ancient-card transcendence of Little Parade, obtained through Archaic Tooth.
/// </summary>
[RegisterCard(typeof(MgrCardPool), StableEntryStem = "vast_world")]
public sealed class VastWorld : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 1m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public VastWorld() : base(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ChannelNote(choiceContext, NoteKind.Attack);
        await ChannelNote(choiceContext, NoteKind.Skill);
        await ChannelNote(choiceContext, NoteKind.Power);
        await ChannelNote(choiceContext, NoteKind.Starry);
        await ChannelNote(choiceContext, NoteKind.Status);
    }

    protected override void OnUpgrade() =>
        DynamicVars["Performance"].UpgradeValueBy(1m);
}
