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
        IconPath: $"{Entry.ResPath}/images/powers/ImagineCreatePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/ImagineCreatePower.png");

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player || Owner.CombatState is null)
            return;

        int changes = Math.Max(0, (int)Amount);
        if (changes > 0)
            await ChangeCardsInOneBatch(choiceContext, player, changes);
    }

    private async Task ChangeCardsInOneBatch(
        PlayerChoiceContext choiceContext,
        Player player,
        int maximumChanges)
    {
        CardModel[] eligibleCards = PileType.Hand.GetPile(player).Cards
            .Where(IsEligible)
            .ToArray();
        if (eligibleCards.Length == 0)
            return;

        using IDisposable screenFilter =
            MgrSelectionScreenVfx.BeginGrayscale(player);

        int selectMaximum = Math.Min(maximumChanges, eligibleCards.Length);
        var chooseCardPrompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_IMAGINE_CREATE_CHOOSE_CARD");
        var chooseCardPrefs = new CardSelectorPrefs(
            chooseCardPrompt,
            0,
            selectMaximum)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        List<CardModel> originals = (await CardSelectCmd.FromHand(
            choiceContext,
            player,
            chooseCardPrefs,
            IsEligible,
            this)).ToList();
        if (originals.Count == 0)
            return;

        var pendingTransforms = new List<(CardModel Original, CardModel Chosen)>();
        var temporaryOptions = new List<CardModel>();
        try
        {
            // Decide every selected card's destination type before replacing
            // anything in the hand. This keeps stacked copies of the Power as
            // one coherent batch instead of repeatedly reopening the hand.
            foreach (CardModel original in originals)
            {
                List<CardModel> options = TypeOptions
                    .Select(type => CreateTypeOption(original, type))
                    .ToList();
                temporaryOptions.AddRange(options);

                var chooseTypePrompt = new LocString(
                    "cards",
                    "SLAY_THE_SPIRE2_MGR_MOD_CARD_IMAGINE_CREATE_CHOOSE_TYPE");
                var chooseTypePrefs = new CardSelectorPrefs(chooseTypePrompt, 1);
                CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    options,
                    player,
                    chooseTypePrefs)).FirstOrDefault();
                if (chosen is not null)
                    pendingTransforms.Add((original, chosen));
            }

            if (pendingTransforms.Count == 0)
                return;

            Flash();
            MgrAbilityVfx.SpawnCastBurst(
                Owner,
                MgrAbilityVfxStyle.Creation,
                0.78f);
            foreach ((CardModel original, CardModel chosen) in pendingTransforms)
                await CardCmd.Transform(original, chosen, CardPreviewStyle.None);
        }
        finally
        {
            // Selected options belong to the hand after Transform. Every other
            // option is a temporary combat model used only by the type grids.
            await Task.Yield();
            foreach (CardModel option in temporaryOptions)
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
