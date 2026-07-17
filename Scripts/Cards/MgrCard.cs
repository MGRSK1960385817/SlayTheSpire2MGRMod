using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;
using SlayTheSpire2MGRMod.Mechanics;

namespace SlayTheSpire2MGRMod.Cards;

/// <summary>
/// Shared base for every MGR card.
/// Card-specific note overrides and future performance duration are declared here,
/// while ordinary cards continue to derive their note from <see cref="CardModel.Type"/>.
/// </summary>
public abstract class MgrCard(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : ModCardTemplate(baseCost, type, rarity, target, showInCardLibrary)
{
    /// <summary>
    /// Marks an MGR card as a Starry card, overriding its ordinary card-type note.
    /// </summary>
    public virtual bool IsStarryCard => false;

    /// <summary>
    /// Initial number of future turn starts on which this card will be performed.
    /// Remaining turns live in the combat-only performance entry so the printed
    /// value and the mutable queue state cannot be confused.
    /// </summary>
    public virtual int InitialPerformanceTurns => 0;

    /// <summary>
    /// Called after the final performance play and its native result-pile routing
    /// resolve. Override this for card-specific finales.
    /// </summary>
    public virtual Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context) => Task.CompletedTask;

    /// <summary>
    /// Optional note override for special MGR cards.
    /// </summary>
    public virtual NoteKind? NoteOverride => IsStarryCard ? NoteKind.Starry : null;

    /// <summary>
    /// True when this card is played into an empty phrase. The automatic note for
    /// the card itself is generated after the play finishes.
    /// </summary>
    protected bool IsPhraseStart => MgrNoteSystem.IsStarting(Owner);

    /// <summary>
    /// True when the card's automatic note will complete the current phrase.
    /// </summary>
    protected bool IsPhraseEnd => MgrNoteSystem.IsEnding(Owner);

    protected MgrCombatState NoteState => MgrCombatStateStore.For(Owner);

    protected Task ChannelNote(PlayerChoiceContext choiceContext, NoteKind kind) =>
        MgrNoteSystem.ChannelNote(choiceContext, Owner, kind);

    /// <summary>
    /// A Performance card is held outside the ordinary combat piles until its
    /// queue entry finishes. The engine's Play pile keeps the model registered
    /// with combat without exposing it to draw/discard/exhaust effects. The last
    /// automatic play is released to Tower 2's normal result-pile routing.
    /// </summary>
    protected override (PileType, CardPilePosition) GetResultPileTypeAndPositionForCardPlay() =>
        MgrPerformanceSystem.IsPerformanceCard(this) &&
        !MgrPerformanceSystem.IsCompletingPerformance(this)
            ? (PileType.Play, CardPilePosition.Bottom)
            : base.GetResultPileTypeAndPositionForCardPlay();

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
}
