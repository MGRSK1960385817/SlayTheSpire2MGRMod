using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class HelloWorldPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/HelloWorldPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/HelloWorldPower.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner || player.Creature.CombatState is not { } combatState)
            return;

        CardModel[] candidates = CardFactory
            .FilterForCombat(player.Character.CardPool.GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint))
            .Where(card => card.CanBeGeneratedInCombat)
            .Where(card => card.EnergyCost.Canonical == 1)
            .ToArray();
        if (candidates.Length == 0)
            return;

        int cards = Math.Max(0, (int)Amount);
        if (cards == 0)
            return;

        Flash();
        for (int index = 0; index < cards; index++)
        {
            CardModel? canonical = MgrWeightedCardRandom.PickOne(
                candidates,
                player.RunState.Rng.CombatCardGeneration,
                MgrCardWeightProfile.GentleCompensation);
            if (canonical is null)
                break;

            CardModel generated = combatState.CreateCard(canonical, player);
            await CardPileCmd.AddGeneratedCardToCombat(
                generated,
                PileType.Hand,
                player);
        }
    }
}
