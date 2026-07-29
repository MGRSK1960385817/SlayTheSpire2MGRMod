using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;

namespace SlayTheSpire2MGRMod.Cards;

[Flags]
public enum MgrGoldGlowCondition
{
    None = 0,
    PhraseStart = 1 << 0,
    PhraseEnd = 1 << 1,
    ChordResolvedThisTurn = 1 << 2,
    NoChordResolvedThisTurn = 1 << 3,
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

    internal MgrKeywordKind DeclaredKeywordKinds => KeywordKinds;
    internal MgrGoldGlowCondition DeclaredGoldGlowConditions => GoldGlowConditions;

#pragma warning disable CS0672
    [Obsolete("RitsuLib uses this channel to seed registered mod CardKeyword values independently of vanilla keywords.")]
    protected override IEnumerable<string> RegisteredKeywordIds =>
        MgrKeywords.GetIds(this);
#pragma warning restore CS0672

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
    /// Resolves Performance for result-pile routing. Most cards use their
    /// printed/combat-modified value; X-cost cards may instead use the resource
    /// snapshot captured for the current play, which exists before OnPlay has
    /// finished updating card-local state.
    /// </summary>
    internal virtual int GetPerformanceTurnsForResultRouting(ResourceInfo resources) =>
        MgrPerformanceSystem.GetInitialPerformanceTurns(this);

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

            bool alwaysPhrase =
                Owner.Creature.GetPowerAmount<DoubleNotesPower>() > 0m;

            return
                conditions.HasFlag(MgrGoldGlowCondition.PhraseStart) &&
                (state.Phrase.IsStarting || alwaysPhrase) ||
                conditions.HasFlag(MgrGoldGlowCondition.PhraseEnd) &&
                (state.Phrase.IsEnding || alwaysPhrase) ||
                conditions.HasFlag(MgrGoldGlowCondition.ChordResolvedThisTurn) &&
                state.ChordsResolvedThisTurn > 0 ||
                conditions.HasFlag(MgrGoldGlowCondition.NoChordResolvedThisTurn) &&
                state.ChordsResolvedThisTurn == 0 ||
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

    protected MgrCombatState NoteState => MgrCombatStateStore.For(Owner);

    protected Task ChannelNote(PlayerChoiceContext choiceContext, NoteKind kind) =>
        MgrNoteSystem.ChannelNote(choiceContext, Owner, kind);

    /// <summary>
    /// A Performance card is held outside the ordinary combat piles until its
    /// queue entry finishes. The engine's Play pile keeps the model registered
    /// with combat without exposing it to draw/discard/exhaust effects. The last
    /// automatic play is released to Tower 2's normal result-pile routing.
    /// </summary>
    protected override CardLocation GetResultLocationForCardPlay() =>
        MgrPerformanceSystem.IsPerformanceCard(this) &&
        !MgrPerformanceSystem.IsCompletingPerformance(this)
            ? new CardLocation(Owner, PileType.Play, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
}
