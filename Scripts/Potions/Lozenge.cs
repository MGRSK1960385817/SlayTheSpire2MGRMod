using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MGRMod.Characters;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Potions;

[RegisterPotion(typeof(MgrPotionPool), StableEntryStem = "lozenge")]
public sealed class Lozenge : MgrPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;

    public override PotionAssetProfile AssetProfile =>
        LocalArt(nameof(Lozenge));

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FortePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<FortePower>()
    ];

    protected override async Task OnUse(
        PlayerChoiceContext choiceContext,
        Creature? target)
    {
        AssertValidForTargetedPotion(target);
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("f4a15d"));
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            target,
            DynamicVars["FortePower"].BaseValue,
            Owner.Creature,
            cardSource: null);
    }
}
