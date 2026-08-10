using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SlayTheSpire2MGRMod.Cards;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class ImagineCreatePower : ModPowerTemplate
{
    private static readonly CardType[] TypeOptions =
    [
        CardType.Attack,
        CardType.Skill,
        CardType.Power,
        CardType.Curse,
        CardType.Status
    ];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/cards/ImagineCreate.png",
        BigIconPath: $"{Entry.ResPath}/images/cards/ImagineCreate.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player || Owner.CombatState is null)
            return;

        int changes = Math.Max(0, (int)Amount);
        for (int index = 0; index < changes; index++)
        {
            if (!await TryChangeOneCard(choiceContext, player))
                break;
        }
    }

    private async Task<bool> TryChangeOneCard(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        CardModel[] eligibleCards = PileType.Hand.GetPile(player).Cards
            .Where(IsEligible)
            .ToArray();
        if (eligibleCards.Length == 0)
            return false;

        var chooseCardPrompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_IMAGINE_CREATE_CHOOSE_CARD");
        var chooseCardPrefs = new CardSelectorPrefs(chooseCardPrompt, 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        CardModel? original = (await CardSelectCmd.FromHand(
            choiceContext,
            player,
            chooseCardPrefs,
            IsEligible,
            this)).FirstOrDefault();
        if (original is null)
            return false;

        List<CardModel> options = TypeOptions
            .Select(type => CreateTypeOption(original, type))
            .ToList();
        CardModel? chosen = null;
        try
        {
            var chooseTypePrompt = new LocString(
                "cards",
                "SLAY_THE_SPIRE2_MGR_MOD_CARD_IMAGINE_CREATE_CHOOSE_TYPE");
            var chooseTypePrefs = new CardSelectorPrefs(chooseTypePrompt, 1);
            chosen = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                options,
                player,
                chooseTypePrefs)).FirstOrDefault();
            if (chosen is null)
                return false;

            Flash();
            await CardCmd.Transform(original, chosen, CardPreviewStyle.None);
            return true;
        }
        finally
        {
            // The selected option belongs to the hand after Transform. The other
            // four are temporary combat models used only by the selection grid.
            await Task.Yield();
            foreach (CardModel option in options)
            {
                if (option.Pile is null && option.CombatState is not null)
                    option.RemoveFromState();
            }
        }
    }

    private static bool IsEligible(CardModel card) => true;

    private static CardModel CreateTypeOption(CardModel original, CardType type)
    {
        CardModel option = (original.CombatState ??
            throw new InvalidOperationException("Type choices require a combat card."))
            .CloneCard(original);
        MgrCardTypeOverrideState.Set(option, type);
        return option;
    }
}
