using MegaCrit.Sts2.Core.Entities.Potions;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Potions;

/// <summary>
/// Common base for potions owned by the MGR character pool.
/// Until bespoke art is available, individual potions may point their asset
/// profiles at vanilla potion textures without copying or resizing them.
/// </summary>
public abstract class MgrPotion : ModPotionTemplate
{
    public override PotionUsage Usage => PotionUsage.CombatOnly;

    protected static PotionAssetProfile VanillaArt(string entryStem) => new(
        $"res://images/atlases/potion_atlas.sprites/{entryStem}.tres",
        $"res://images/atlases/potion_outline_atlas.sprites/{entryStem}.tres");
}
