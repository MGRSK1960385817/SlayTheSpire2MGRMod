using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "daybreak_frontline")]
public sealed class DaybreakFrontline : MgrCard
{
    protected override bool TransformsCardsIntoNotes => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 3m)
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
            await MgrAbilityVfx.PlayCentralCardExhaust(targets);

        foreach (CardModel target in targets)
        {
            NoteKind kind = CardNoteResolver.Resolve(target);
            MgrPerformanceSystem.DetachQueuedCard(context.Player, target);

            CardPile? sourcePile = target.Pile;
            if (sourcePile?.Type is PileType.Hand &&
                NPlayerHand.Instance is { } hand &&
                hand.GetCardHolder(target) is not null)
            {
                // Visual presentation normally transfers the real hand NCard
                // into the central exhaust row. Keep this fallback for test or
                // headless paths where no combat UI was available.
                hand.Remove(target);
            }

            if (sourcePile?.Type is PileType.Exhaust)
            {
                // Re-exhaust cards which already live in the Exhaust pile: lift
                // the model out silently, then let the real command put it back
                // so Before/AfterExhaust hooks run once more. The pile ends with
                // the same count, matching the requested "out, then back in".
                sourcePile.RemoveInternal(target, silent: true);
            }

            await CardCmd.Exhaust(
                choiceContext,
                target,
                skipVisuals: true);
            NotifySilentExhaustFinished(sourcePile, target);
            await MgrNoteSystem.ChannelNote(choiceContext, context.Player, kind);
        }
    }

    /// <summary>
    /// skipVisuals suppresses the native NCard flight which normally emits
    /// these callbacks. Restore both sides after the real Exhaust command and
    /// all of its per-card hooks have completed, so pile counters match their
    /// underlying piles immediately. Hand node removal is handled explicitly
    /// before the command by the central presentation (or its headless fallback)
    /// because a completion notification alone cannot remove an NCard/holder pair.
    /// </summary>
    private static void NotifySilentExhaustFinished(
        CardPile? sourcePile,
        CardModel exhaustedCard)
    {
        sourcePile?.InvokeCardRemoveFinished();
        if (exhaustedCard.Pile is { Type: PileType.Exhaust } exhaustPile)
            exhaustPile.InvokeCardAddFinished();
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
