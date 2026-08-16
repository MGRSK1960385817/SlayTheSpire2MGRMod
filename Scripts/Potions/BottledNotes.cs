using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Potions;

[RegisterPotion(typeof(MgrPotionPool), StableEntryStem = "bottled_notes")]
public sealed class BottledNotes : MgrPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override TargetType TargetType => TargetType.Self;

    public override PotionAssetProfile AssetProfile =>
        LocalArt(nameof(BottledNotes));

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 16m)
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
