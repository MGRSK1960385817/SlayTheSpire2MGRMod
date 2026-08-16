using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Potions;

[RegisterPotion(typeof(MgrPotionPool), StableEntryStem = "soul_of_stage")]
public sealed class SoulOfStage : MgrPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override TargetType TargetType => TargetType.Self;

    public override PotionAssetProfile AssetProfile =>
        LocalArt(nameof(SoulOfStage));

    protected override async Task OnUse(
        PlayerChoiceContext choiceContext,
        Creature? target)
    {
        AssertValidForTargetedPotion(target);
        ArgumentNullException.ThrowIfNull(target.Player);
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("b18cff"));

        CardModel[] handSnapshot = PileType.Hand
            .GetPile(target.Player)
            .Cards
            .ToArray();
        foreach (CardModel card in handSnapshot)
            await MgrPerformanceSystem.EnqueueCardFromHand(target.Player, card);
    }
}
