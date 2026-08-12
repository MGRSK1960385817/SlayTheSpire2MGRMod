using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class ChaosMagicPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/cards/ChaosMagic.png",
        BigIconPath: $"{Entry.ResPath}/images/cards/ChaosMagic.png");

    public async Task OnPerformanceEnded(Player player)
    {
        if (player.Creature != Owner || player.Creature.CombatState is not { } combatState)
            return;

        CardModel[] candidates = CardFactory
            .FilterForCombat(player.Character.CardPool.GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint))
            .OfType<MgrCard>()
            .Where(IsPrintedPerformanceCard)
            .Where(card => card.CanBeGeneratedInCombat)
            .Cast<CardModel>()
            .ToArray();
        if (candidates.Length == 0)
            return;

        int cards = Math.Max(0, (int)Amount);
        if (cards == 0)
            return;

        Flash();
        MgrAbilityVfx.SpawnCastBurst(
            Owner,
            MgrAbilityVfxStyle.Wheel,
            0.76f);
        for (int index = 0; index < cards; index++)
        {
            CardModel? canonical = player.RunState.Rng.CombatCardGeneration.NextItem(candidates);
            if (canonical is null)
                break;

            CardModel generated = combatState.CreateCard(canonical, player);
            await CardPileCmd.AddGeneratedCardToCombat(
                generated,
                PileType.Hand,
                player);
        }
    }

    private static bool IsPrintedPerformanceCard(MgrCard card) =>
        card.InitialPerformanceTurns > 0 || card is CubicPrism or LightSong;
}
