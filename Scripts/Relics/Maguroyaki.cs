using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "maguroyaki")]
public sealed class Maguroyaki : ModRelicTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(15m)];

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/Maguroyaki.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/Maguroyaki_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/Maguroyaki.png");

    public override async Task AfterRestSiteSmith(Player player)
    {
        if (player != Owner)
            return;

        Flash();
        decimal amount = player.Creature.MaxHp * (DynamicVars.Heal.BaseValue / 100m);
        await CreatureCmd.Heal(player.Creature, amount);
    }
}
