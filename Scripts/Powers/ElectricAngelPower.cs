using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class ElectricAngelPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/ElectricAngelPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/ElectricAngelPower.png");

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
        MgrAbilityVfx.SpawnCastBurst(
            Owner,
            MgrAbilityVfxStyle.Electric,
            0.68f);
        for (int index = 0; index < cards; index++)
        {
            CardModel? canonical = MgrWeightedCardRandom.PickOne(
                candidates,
                player.RunState.Rng.CombatCardGeneration,
                MgrCardWeightProfile.ElectricAngel);
            if (canonical is null)
                break;

            CardModel generated = combatState.CreateCard(canonical, player);
            await MgrAbilityVfx.PlayElectricAngelCardGeneration(generated);
            await CardPileCmd.AddGeneratedCardToCombat(
                generated,
                PileType.Hand,
                player);
            await Cmd.Wait(0.08f);
        }
    }
}
