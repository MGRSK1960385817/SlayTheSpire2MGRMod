using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "magic_broom")]
public sealed class MagicBroom : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [MgrHoverTips.TransformIntoNote()];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/MagicBroom.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/MagicBroom_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/MagicBroom.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext, Player player)
    {
        // Like Gambling Chip, resolve opening-hand choices after the first draw.
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber != 1 ||
            MgrNoteSystem.ShouldStopNoteSequence(Owner))
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner);

        if (MgrNoteSystem.ShouldStopNoteSequence(Owner) ||
            !PileType.Hand.GetPile(Owner).Cards.Any(card => card.Type != CardType.None))
            return;

        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext, Owner, new CardSelectorPrefs(SelectionScreenPrompt, 1),
            filter: card => card.Type != CardType.None, source: this)).FirstOrDefault();
        if (selected is null || MgrNoteSystem.ShouldStopNoteSequence(Owner))
            return;

        NoteKind kind = CardNoteResolver.Resolve(selected);
        await CardCmd.Exhaust(choiceContext, selected);
        await MgrNoteSystem.ResolveCardTransformation(choiceContext, Owner, kind);
    }
}
