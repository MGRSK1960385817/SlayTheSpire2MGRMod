using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Potions;

[RegisterPotion(typeof(MgrPotionPool), StableEntryStem = "improvisation_potion")]
public sealed class ImprovisationPotion : MgrPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;

    // Temporary art: the Regent's Star Potion.
    public override PotionAssetProfile AssetProfile =>
        VanillaArt("star_potion");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 4m)
    ];

    protected override async Task OnUse(
        PlayerChoiceContext choiceContext,
        Creature? target)
    {
        AssertValidForTargetedPotion(target);
        ArgumentNullException.ThrowIfNull(target.Player);
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("d68cff"));

        for (int index = 0; index < DynamicVars["Notes"].IntValue; index++)
            await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, target.Player);
    }
}
