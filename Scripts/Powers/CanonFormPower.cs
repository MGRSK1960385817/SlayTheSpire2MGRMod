using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MGRMod.Cards;
using MGRMod.Compatibility;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class CanonFormPower : ModPowerTemplate
{
    private int _pendingStacks;
    private int _pendingActivationTurn;
    private bool _isResolving;
    private int _visualTriggerSerial;

    /// <summary>
    /// Transient presentation counter observed by MGR's character-local aura.
    /// It is deliberately not saved: loading/re-entering a combat recreates the
    /// idle wheel, while only a real replay batch advances the wheel one turn.
    /// </summary>
    public int VisualTriggerSerial => _visualTriggerSerial;

    [SavedProperty]
    public int PendingStacks
    {
        get => _pendingStacks;
        set
        {
            AssertMutable();
            _pendingStacks = Math.Max(0, value);
        }
    }

    [SavedProperty]
    public int PendingActivationTurn
    {
        get => _pendingActivationTurn;
        set
        {
            AssertMutable();
            _pendingActivationTurn = Math.Max(0, value);
        }
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/CanonFormPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/CanonFormPower.png");

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            CardModel[] cards = GetNextReplayCardsForDescription();
            description.Add("HasCards", cards.Length > 0);
            description.Add(
                "Cards",
                string.Join(" → ", cards.Select(static card => card.Title)));
            return description;
        }
    }

    /// <summary>
    /// A newly played stack starts working on the following turn. Keeping pending
    /// stacks separate also prevents a copied Canon Form from expanding the batch
    /// that is already being resolved.
    /// </summary>
    public void QueueNewStack(int currentTurn)
    {
        if (PendingStacks > 0 && PendingActivationTurn <= currentTurn)
            PendingStacks = 0;

        if (PendingStacks == 0)
            PendingActivationTurn = currentTurn + 1;

        PendingStacks++;
    }

    public override async Task AfterAutoPostPlayPhaseEntered(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner || _isResolving)
            return;

        PlayerCombatState? playerCombatState = player.PlayerCombatState;
        if (playerCombatState is null)
            return;

        int currentTurn = playerCombatState.TurnNumber;
        if (PendingStacks > 0 && PendingActivationTurn <= currentTurn)
            PendingStacks = 0;

        int activeStacks = GetActiveStackCount(currentTurn);
        if (activeStacks == 0)
            return;

        // Snapshot both the cards and the active stack count before autoplay.
        // Copies played below are recorded normally and may add Canon Form stacks,
        // but those additions only affect the next turn.
        CardModel[] cardsToReplay = GetOrderedReplayCards(player, activeStacks);
        if (cardsToReplay.Length == 0)
            return;

        _isResolving = true;
        try
        {
            Flash();
            _visualTriggerSerial++;
            MgrAbilityVfx.SpawnCastBurst(
                Owner,
                MgrAbilityVfxStyle.Echo,
                0.82f);
            foreach (CardModel card in cardsToReplay)
            {
                if (CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead)
                    break;

                await CardCmd.AutoPlay(
                    choiceContext,
                    MgrCrossVersionApi.CreateDupeForPlayer(card, player),
                    null);
            }
        }
        finally
        {
            _isResolving = false;
        }
    }

    private int GetActiveStackCount(int currentTurn) => Math.Max(
        0,
        (int)Amount -
        (PendingStacks > 0 && PendingActivationTurn > currentTurn
            ? PendingStacks
            : 0));

    private CardModel[] GetNextReplayCardsForDescription()
    {
        if (!IsMutable || !CombatManager.Instance.IsInProgress ||
            Owner is not { Player: { } player } ||
            player.PlayerCombatState is not { } playerCombatState)
        {
            return [];
        }

        return GetOrderedReplayCards(
            player,
            GetActiveStackCount(playerCombatState.TurnNumber));
    }

    private static CardModel[] GetOrderedReplayCards(
        Player player,
        int cardCount)
    {
        if (cardCount <= 0)
            return [];

        CardModel[] cards = CombatManager.Instance.History.CardPlaysFinished
            .Where(entry =>
                entry.CardPlay.Card.Owner == player &&
                entry.HappenedLastPlayerTurn(player))
            .Select(entry => entry.CardPlay.Card)
            .TakeLast(cardCount)
            .ToArray();

        // A canon is cyclic: when Canon Form is part of the selected phrase,
        // rotate the phrase so both the displayed list and actual replay begin
        // with it while preserving every card's relative order.
        int canonIndex = Array.FindIndex(
            cards,
            static card => card is CanonForm);
        if (canonIndex <= 0)
            return cards;

        return cards
            .Skip(canonIndex)
            .Concat(cards.Take(canonIndex))
            .ToArray();
    }
}
