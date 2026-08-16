using MegaCrit.Sts2.Core.Entities.Potions;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Potions;

/// <summary>
/// Common base for potions owned by the MGR character pool.
/// Potion art and its outline source are both stored inside the MGR package;
/// no potion depends on a vanilla or another mod's texture path.
/// </summary>
public abstract class MgrPotion : ModPotionTemplate
{
    public override PotionUsage Usage => PotionUsage.CombatOnly;

    protected static PotionAssetProfile LocalArt(string codeName)
    {
        string imagePath = $"{Entry.ResPath}/images/potion/{codeName}.png";
        string outlinePath = $"{Entry.ResPath}/images/potion/{codeName}_outline.png";
        // The outline is an expanded white alpha mask, matching the vanilla
        // potion atlas. Potion Lab then tints it with MgrPotionPool.LabOutlineColor.
        return new PotionAssetProfile(imagePath, outlinePath);
    }
}
