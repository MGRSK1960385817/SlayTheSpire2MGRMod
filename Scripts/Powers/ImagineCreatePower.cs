using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MGRMod.Cards;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

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
            "MGR_MOD_CARD_IMAGINE_CREATE_CHOOSE_CARD");
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
        List<NCardHolder> hiddenSelectedHolders =
            HideSelectedHandCards(originals);
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

                CardModel? chosen = await MgrWideCardSelectCmd.FromChooseACardScreen(
                    choiceContext,
                    options,
                    player);
                if (chosen is not null)
                    pendingTransforms.Add((original, chosen));
            }

            if (pendingTransforms.Count == 0)
                return;

            RestoreSelectedHandCards(hiddenSelectedHolders);
            hiddenSelectedHolders.Clear();
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
            RestoreSelectedHandCards(hiddenSelectedHolders);
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

    /// <summary>
    /// FromHand intentionally keeps selected holders in its raised selection
    /// container until the caller consumes or moves those cards. Imagine/Create
    /// opens another choice screen first, so temporarily hide those holders to
    /// keep the selected hand cards from covering the five type candidates.
    /// </summary>
    private static List<NCardHolder> HideSelectedHandCards(
        IEnumerable<CardModel> cards)
    {
        var hidden = new List<NCardHolder>();
        if (NPlayerHand.Instance is not { } hand)
            return hidden;

        foreach (CardModel card in cards)
        {
            if (hand.GetCardHolder(card) is not { } holder ||
                !GodotObject.IsInstanceValid(holder))
            {
                continue;
            }

            holder.Visible = false;
            hidden.Add(holder);
        }

        return hidden;
    }

    private static void RestoreSelectedHandCards(
        IEnumerable<NCardHolder> holders)
    {
        foreach (NCardHolder holder in holders)
        {
            if (GodotObject.IsInstanceValid(holder))
                holder.Visible = true;
        }
    }

    private static bool IsEligible(CardModel card) => true;

    private static CardModel CreateTypeOption(CardModel original, CardType type)
    {
        CardModel option = (original.CombatState ??
            throw new InvalidOperationException("Type choices require a combat card."))
            .CloneCard(original);
        if (option.Affliction is Tainted)
            CardCmd.ClearAffliction(option);
        MgrCardTypeOverrideState.Set(option, type);
        return option;
    }
}
