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
[RegisterCard(typeof(MgrCardPool), StableEntryStem = "omnia")]
public sealed class Omnia : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 3m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;
    public override NoteKind? NoteOverride => NoteKind.OmniaNote;

    public Omnia() : base(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade() =>
        DynamicVars["Performance"].UpgradeValueBy(1m);
}
