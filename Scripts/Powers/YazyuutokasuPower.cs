using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using SlayTheSpire2MGRMod.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public class YazyuutokasuPower : ModPowerTemplate
{
    protected virtual bool CreatesUpgradedConfused => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/YazyuutokasuPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/YazyuutokasuPower.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player || Owner.CombatState is not { } combatState)
            return;

        int amount = Math.Max(0, (int)Amount);
        if (amount == 0)
            return;

        Flash();
        for (int index = 0; index < amount; index++)
        {
            CardModel[] hand = PileType.Hand.GetPile(player).Cards.ToArray();
            if (hand.Length > 0)
            {
                int lowestCost = hand.Min(card => card.EnergyCost.GetResolved());
                CardModel discarded = player.RunState.Rng.CombatCardSelection.NextItem(
                    hand.Where(card => card.EnergyCost.GetResolved() == lowestCost))
                    ?? throw new InvalidOperationException("No lowest-cost card was available to discard.");

                await RevealBeforeDiscard(discarded);
                await CardCmd.Discard(choiceContext, discarded);
            }

            Confused confused = combatState.CreateCard<Confused>(player);
            if (CreatesUpgradedConfused)
                CardCmd.Upgrade(confused, CardPreviewStyle.None);
            CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
                confused,
                PileType.Hand,
                player);
            CardCmd.PreviewCardPileAdd(result);
        }
    }

    private static async Task RevealBeforeDiscard(CardModel card)
    {
        NCard? cardNode = NPlayerHand.Instance?.GetCard(card);
        if (cardNode is null ||
            !GodotObject.IsInstanceValid(cardNode) ||
            !cardNode.IsInsideTree())
        {
            return;
        }

        cardNode.PivotOffset = cardNode.Size * 0.5f;
        cardNode.ZIndex = 500;
        Vector2 raisedPosition = cardNode.Position + new Vector2(0f, -72f);
        Vector2 raisedScale = cardNode.Scale * 1.08f;

        Tween tween = cardNode.CreateTween().SetParallel();
        tween.TweenProperty(cardNode, "position", raisedPosition, 0.14)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(cardNode, "scale", raisedScale, 0.14)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(cardNode, "modulate", new Color(1.2f, 1.12f, 1.24f), 0.14);
        tween.Chain().TweenInterval(0.22);

        await TweenHelper.AwaitFinished(tween, cardNode);
    }
}
