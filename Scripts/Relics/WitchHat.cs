using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "witch_hat")]
public sealed class WitchHat : ModRelicTemplate
{
    private const decimal BaseBlock = 2m;
    private const decimal BloodiedBonusBlock = 2m;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/WitchHat.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/WitchHat_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/WitchHat.png");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Creature.Side)
            return;

        decimal block = BaseBlock;
        if (Owner.Creature.CurrentHp * 2m <= Owner.Creature.MaxHp)
            block += BloodiedBonusBlock;

        Flash();
        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Unpowered,
            cardPlay: null);
    }
}
