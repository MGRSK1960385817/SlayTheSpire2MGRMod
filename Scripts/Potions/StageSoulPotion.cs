using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Potions;

[RegisterPotion(typeof(MgrPotionPool), StableEntryStem = "stage_soul_potion")]
public sealed class StageSoulPotion : MgrPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;

    // Temporary art: Ghost in a Jar.
    public override PotionAssetProfile AssetProfile =>
        VanillaArt("ghost_in_a_jar");

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
