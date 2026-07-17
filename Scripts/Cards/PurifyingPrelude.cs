using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "purifying_prelude")]
public sealed class PurifyingPrelude : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 3m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public PurifyingPrelude() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    public override async Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context)
    {
        CardModel[] targets = MgrCurseUtils.SnapshotCursesAndStatuses(
            context.Player,
            PileType.Draw,
            PileType.Discard);

        foreach (CardModel target in targets)
        {
            NoteKind kind = CardNoteResolver.Resolve(target);
            await CardCmd.Exhaust(choiceContext, target);
            await MgrNoteSystem.ChannelNote(choiceContext, context.Player, kind);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(-1m);
    }
}
