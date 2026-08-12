using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "daybreak_frontline")]
public sealed class DaybreakFrontline : MgrCard
{
    protected override bool TransformsCardsIntoNotes => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 4m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        MgrHoverTips.CardsInCombat()
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public DaybreakFrontline() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
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
            includePerformanceQueue: true,
            PileType.Hand,
            PileType.Draw,
            PileType.Discard,
            PileType.Exhaust);

        if (targets.Length > 0)
        {
            CardCmd.Preview(
                targets,
                time: 0.62f,
                style: CardPreviewStyle.MessyLayout);
            await Cmd.Wait(0.42f);
            MgrAbilityVfx.PlayCentralPurification(targets.Length);
        }

        foreach (CardModel target in targets)
        {
            NoteKind kind = CardNoteResolver.Resolve(target);
            MgrPerformanceSystem.DetachQueuedCard(context.Player, target);
            await CardCmd.Exhaust(choiceContext, target, skipVisuals: true);
            await MgrNoteSystem.ChannelNote(choiceContext, context.Player, kind);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
