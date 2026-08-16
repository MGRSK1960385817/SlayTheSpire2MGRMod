using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "of_all_beings")]
public sealed class OfAllBeings : MgrCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    protected override MgrKeywordKind KeywordKinds => MgrKeywordKind.Performance;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat(
        [
            CardKeyword.Retain,
            CardKeyword.Exhaust
        ]);

    public OfAllBeings() : base(
        2,
        CardType.Skill,
        CardRarity.Rare,
        TargetType.AnyAlly)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (CombatState is null)
            return;

        var targetPlayer = cardPlay.Target.Player ?? throw new InvalidOperationException(
            "Of All Beings requires a player target.");
        CardModel[] cardsPlayedByTarget = CombatManager.Instance.History
            .CardPlaysFinished
            .Where(entry =>
                entry.HappenedThisTurn(CombatState) &&
                entry.CardPlay.Player == targetPlayer)
            .Select(entry => entry.CardPlay.Card)
            .ToArray();

        foreach (CardModel source in cardsPlayedByTarget)
        {
            CardModel copy = source.CreateCloneForPlayer(Owner);
            copy.AddKeyword(CardKeyword.Exhaust);
            await MgrPerformanceSystem.EnqueueGeneratedCard(Owner, copy);
        }

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
