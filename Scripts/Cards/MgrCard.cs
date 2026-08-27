using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;
using MGRMod.Mechanics;

namespace MGRMod.Cards;

[Flags]
public enum MgrGoldGlowCondition
{
    None = 0,
    PhraseStart = 1 << 0,
    PhraseEnd = 1 << 1,
    ChordTriggeredThisTurn = 1 << 2,
    NoChordTriggeredThisTurn = 1 << 3,
    AtLeastTwoNotes = 1 << 4
}

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
    /// Extra concepts named by this card's rules text. Identity keywords such as
    /// Starry and Performance, plus gold-glow phrase conditions, are added by the
    /// shared base automatically.
    /// </summary>
    protected virtual MgrKeywordKind KeywordKinds => MgrKeywordKind.None;

    /// <summary>
    /// Adds the shared rules reminder to cards which turn existing cards into
    /// Notes. The reminder is supplemental rather than a gameplay keyword, so
    /// it is displayed after the card's ordinary keyword explanations.
    /// </summary>
    protected virtual bool TransformsCardsIntoNotes => false;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        TransformsCardsIntoNotes
            ? [MgrHoverTips.TransformIntoNote()]
            : [];

    internal MgrKeywordKind DeclaredKeywordKinds => KeywordKinds;
    internal MgrGoldGlowCondition DeclaredGoldGlowConditions => GoldGlowConditions;

    /// <summary>
    /// RitsuLib 0.5.1 stores mod keywords directly in Tower 2's native keyword
    /// collection. Preserve any canonical vanilla keywords and append MGR's
    /// registered values through the new deterministic CardKeyword mapping.
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat(
            MgrKeywords.GetIds(this).Select(id => id.GetModCardKeyword()));

    /// <summary>
    /// Marks an MGR card as a Starry card, overriding its ordinary card-type note.
    /// </summary>
    public virtual bool IsStarryCard => false;

    /// <summary>
    /// Initial number of future turn starts on which this card will be performed.
    /// Remaining turns live in the combat-only performance entry so the printed
    /// value and the mutable queue state cannot be mixed up.
    /// </summary>
    public virtual int InitialPerformanceTurns => 0;

    /// <summary>
    /// Resolves Performance for result-pile routing. Most cards use their
    /// printed/combat-modified value; X-cost cards may instead use the resource
    /// snapshot captured for the current play, which exists before OnPlay has
    /// finished updating card-local state.
    /// </summary>
    internal virtual int GetPerformanceTurnsForResultRouting(ResourceInfo resources) =>
        MgrPerformanceSystem.GetInitialPerformanceTurns(this);

    /// <summary>
    /// Returns the number of remaining Performance turns this card will grant to
    /// its own live queue entry during the current automatic play. The scheduler
    /// previews this value before result-pile routing, then applies it only after
    /// the card play succeeds.
    /// </summary>
    internal virtual int GetCurrentAutoPlayPerformanceExtension() => 0;

    /// <summary>
    /// Declares the combat condition that makes this card stronger and should
    /// therefore use Tower 2's native gold hand-card glow. Multiple flags use
    /// OR semantics: the card glows when any declared bonus is currently active.
    /// </summary>
    protected virtual MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.None;

    /// <summary>
    /// Uses the same native hook as cards such as Evil Eye. Keeping the mapping
    /// here makes future MGR conditional cards opt in with one declarative line.
    /// </summary>
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            MgrGoldGlowCondition conditions = GoldGlowConditions;
            if (conditions == MgrGoldGlowCondition.None ||
                CombatState is null ||
                !MgrCombatStateStore.TryGet(Owner, out MgrCombatState state))
            {
                return false;
            }

            return
                conditions.HasFlag(MgrGoldGlowCondition.PhraseStart) &&
                state.Phrase.IsStarting ||
                conditions.HasFlag(MgrGoldGlowCondition.PhraseEnd) &&
                state.Phrase.IsEnding ||
                conditions.HasFlag(MgrGoldGlowCondition.ChordTriggeredThisTurn) &&
                state.ChordTriggersThisTurn > 0 ||
                conditions.HasFlag(MgrGoldGlowCondition.NoChordTriggeredThisTurn) &&
                state.ChordTriggersThisTurn == 0 ||
                conditions.HasFlag(MgrGoldGlowCondition.AtLeastTwoNotes) &&
                state.Phrase.Notes.Count >= 2;
        }
    }

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

    /// <summary>
    /// Shared condition for effects whose final value is doubled by Ending.
    /// Phrase-edge bonuses use the actual note-slot state.
    /// </summary>
    internal bool IsPhraseEndBonusActive =>
        CombatState is not null &&
        IsPhraseEnd;

    protected MgrCombatState NoteState => MgrCombatStateStore.For(Owner);

    protected Task ChannelNote(PlayerChoiceContext choiceContext, NoteKind kind) =>
        MgrNoteSystem.ChannelNote(choiceContext, Owner, kind);

    /// <summary>
    /// A Performance card is held outside the ordinary combat piles until its
    /// queue entry finishes. The engine's Play pile keeps the model registered
    /// with combat without exposing it to draw/discard/exhaust effects. The last
    /// automatic play is released to Tower 2's normal result-pile routing.
    /// </summary>
#if STS2_V107
    protected override PileType GetResultPileTypeForCardPlay() =>
        MgrPerformanceSystem.IsPerformanceCard(this) &&
        !MgrPerformanceSystem.IsCompletingPerformance(this)
            ? PileType.Play
            : base.GetResultPileTypeForCardPlay();
#else
    protected override CardLocation GetResultLocationForCardPlay() =>
        MgrPerformanceSystem.IsPerformanceCard(this) &&
        !MgrPerformanceSystem.IsCompletingPerformance(this)
            ? new CardLocation(Owner, PileType.Play, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();
#endif

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
}
