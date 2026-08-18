using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MGRMod.Compatibility;

namespace MGRMod.Mechanics;

/// <summary>
/// Uses the native Choose-a-Card overlay without CardSelectCmd's three-card
/// guard. The underlying overlay lays cards out from their count and supports
/// Imagine/Create's five compact candidates; this wrapper preserves the same
/// test-selector and multiplayer choice synchronization as the native command.
/// </summary>
internal static class MgrWideCardSelectCmd
{
    public static async Task<CardModel?> FromChooseACardScreen(
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        Player player)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(player);
        if (cards.Count == 0)
            return null;

        UndoEndTurnIfNecessary(player);
        if (CardSelectCmd.Selector is { } selector)
        {
            return (await selector.GetSelectedCards(cards, 1, 1))
                .FirstOrDefault();
        }

        var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
        uint choiceId = synchronizer.ReserveChoiceId(player);
        await MgrCrossVersionApi.SignalPlayerChoiceBegun(
            context,
            player,
            PlayerChoiceOptions.CancelPlayCardActions);

        CardModel? result = null;
        try
        {
            if (CardSelectCmd.ShouldSelectLocalCard(player))
            {
                if (MgrCrossVersionApi.GetLocalCardSelector() is { } localSelector)
                {
                    result = (await localSelector.GetSelectedCards(cards, 1, 1))
                        .FirstOrDefault();
                }
                else
                {
                    NPlayerHand.Instance?.CancelAllCardPlay();
                    NChooseACardSelectionScreen screen =
                        NChooseACardSelectionScreen.ShowScreen(
                            cards,
                            canSkip: false) ??
                        throw new InvalidOperationException(
                            "Could not create the five-card choice screen.");
                    if (LocalContext.IsMe(player))
                    {
                        foreach (CardModel card in cards)
                            SaveManager.Instance.MarkCardAsSeen(card);
                    }

                    result = (await screen.CardsSelected()).FirstOrDefault();
                }

                synchronizer.SyncLocalChoice(
                    player,
                    choiceId,
                    PlayerChoiceResult.FromIndex(IndexOf(cards, result)));
            }
            else
            {
                int index = (await synchronizer.WaitForRemoteChoice(
                    player,
                    choiceId)).AsIndex();
                result = cards[index];
            }

            CardSelectCmd.LogChoice(
                player,
                result is null ? [] : [result]);
            return result;
        }
        finally
        {
            await context.SignalPlayerChoiceEnded();
        }
    }

    private static int? IndexOf(
        IReadOnlyList<CardModel> cards,
        CardModel? selected)
    {
        if (selected is null)
            return null;

        for (int index = 0; index < cards.Count; index++)
        {
            if (ReferenceEquals(cards[index], selected))
                return index;
        }

        return null;
    }

    private static void UndoEndTurnIfNecessary(Player player)
    {
        if (CombatManager.Instance.IsPlayerReadyToEndTurn(player) &&
            player.Creature.CombatState is { CurrentSide: CombatSide.Player })
        {
            CombatManager.Instance.UndoReadyToEndTurn(player);
        }
    }
}
