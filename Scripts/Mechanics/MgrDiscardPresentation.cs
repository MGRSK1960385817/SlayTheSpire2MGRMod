using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MGRMod.Mechanics;

/// <summary>
/// Makes automatic post-draw discards readable without replacing Tower 2's
/// native hand-to-discard animation. Selected cards flash red briefly, then all
/// move through the same bulk discard command used by Scrape.
/// </summary>
public static class MgrDiscardPresentation
{
    private const float ReadablePauseSeconds = 0.38f;

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
            var highlighted = new List<(NCardHighlight Node, Color OriginalColor)>();
            try
            {
                foreach (CardModel card in discardCards)
                {
                    if (hand.GetCard(card) is not { } cardNode)
                        continue;

                    NCardHighlight highlight = cardNode.CardHighlight;
                    highlighted.Add((highlight, highlight.Modulate));
                    highlight.Modulate = NCardHighlight.red;
                    highlight.AnimFlash();
                }

                // Let the newly drawn cards settle and the red flash become
                // unmistakable before the native parallel discard begins.
                await Cmd.Wait(MgrVisualTiming.ScaleBlockingVisualWait(
                    discardCards[0].Owner,
                    ReadablePauseSeconds));
            }
            finally
            {
                // Hand holders and highlights are pooled. Leaving the red
                // modulation behind makes unrelated cards glow red when those
                // nodes are reused later, so always restore both color and
                // shader width before cards leave the hand.
                foreach ((NCardHighlight highlight, Color originalColor) in highlighted)
                {
                    if (!GodotObject.IsInstanceValid(highlight))
                        continue;

                    highlight.AnimHideInstantly();
                    highlight.Modulate = originalColor;
                }
            }
        }

        // Scrape uses this bulk overload. Besides moving all selected cards in
        // one readable group, it preserves the correct timing for Sly hooks.
        await CardCmd.Discard(choiceContext, discardCards);
    }
}
