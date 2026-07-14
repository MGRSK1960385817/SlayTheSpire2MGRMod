using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
    /// Number of future turn starts on which this card will be played again.
    /// Zero means this is not a Performance card. Runtime scheduling is implemented separately.
    /// </summary>
    public virtual int PerformanceTurns => 0;

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

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
}
