using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "pale_flower")]
public sealed class PaleFlower : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/PaleFlower.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/PaleFlower_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/PaleFlower.png");

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        Flash();
        CardModel pale = combatState.CreateCard(ModelDb.Card<Pale>(), Owner);
        await CardPileCmd.AddGeneratedCardToCombat(pale, PileType.Hand, Owner);
    }
}
