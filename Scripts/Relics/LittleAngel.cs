using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "little_angel")]
public sealed class LittleAngel : ModRelicTemplate
{
    private int _skillsDrawnByHandDraw;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("SkillThreshold", 2m),
        new CardsVar(3)
    ];

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/LittleAngel.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/LittleAngel_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/LittleAngel.png");

    public override Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player == Owner)
            _skillsDrawnByHandDraw = 0;

        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (fromHandDraw && card.Owner == Owner && card.Type == CardType.Skill)
            _skillsDrawnByHandDraw++;

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner || _skillsDrawnByHandDraw >= DynamicVars["SkillThreshold"].IntValue)
            return;

        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);
    }
}
