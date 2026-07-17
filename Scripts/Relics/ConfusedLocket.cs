using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "confused_locket")]
public sealed class ConfusedLocket : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/ConfusedLocket.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/ConfusedLocket_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/ConfusedLocket.png");

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        Confused confused = combatState.CreateCard<Confused>(Owner);
        CardCmd.Upgrade(confused, CardPreviewStyle.None);
        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            confused,
            PileType.Hand,
            Owner);
        Flash();
        CardCmd.PreviewCardPileAdd(result);
    }
}
