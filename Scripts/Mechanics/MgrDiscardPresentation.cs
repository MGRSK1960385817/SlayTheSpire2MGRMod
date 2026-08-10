using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Makes automatic post-draw discards readable without replacing Tower 2's
/// native hand-to-discard animation. Selected cards flash red briefly, then all
/// move through the same bulk discard command used by Scrape.
/// </summary>
public static class MgrDiscardPresentation
{
    private const float ReadablePauseSeconds = 0.24f;

    public static async Task DiscardWithPreview(
        PlayerChoiceContext choiceContext,
        IEnumerable<CardModel> cards)
    {
        CardModel[] discardCards = cards
            .Where(card => card.Pile?.Type == PileType.Hand)
            .ToArray();
        if (discardCards.Length == 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        if (NPlayerHand.Instance is { } hand)
        {
            foreach (CardModel card in discardCards)
            {
                if (hand.GetCard(card) is not { } cardNode)
                    continue;

                cardNode.CardHighlight.Modulate = NCardHighlight.red;
                cardNode.CardHighlight.AnimFlash();
            }

            // Let the newly drawn cards settle and the red flash become visible
            // before the native parallel flight to the discard pile begins.
            await Cmd.Wait(ReadablePauseSeconds);
        }

        // Scrape uses this bulk overload. Besides moving all selected cards in
        // one readable group, it preserves the correct timing for Sly hooks.
        await CardCmd.Discard(choiceContext, discardCards);
    }
}
