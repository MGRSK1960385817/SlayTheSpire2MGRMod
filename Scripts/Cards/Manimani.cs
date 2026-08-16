using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Cards;

/// <summary>
/// A Fatal attack which permanently remembers the unblocked Move damage dealt
/// by its victim during the current combat. Its combat portrait and rules text
/// change only while the player is actively targeting a valid lethal victim.
/// </summary>
[RegisterCard(typeof(MgrCardPool), StableEntryStem = "manimani")]
public sealed class Manimani : MgrCard
{
    private const int BaseDamage = 10;
    private const int UpgradedDamageBonus = 5;
    private const string NormalPortraitPath =
        $"{Entry.ResPath}/images/cards/Manimani1.png";
    private const string FatalPortraitPath =
        $"{Entry.ResPath}/images/cards/Manimani2.png";

    private int _currentDamage = BaseDamage;
    private int _increasedDamage;
    private bool _showFatalPreview;

    [SavedProperty]
    public int CurrentDamage
    {
        get => _currentDamage;
        private set
        {
            AssertMutable();
            _currentDamage = Math.Max(0, value);
            DynamicVars.Damage.BaseValue = _currentDamage;
        }
    }

    [SavedProperty]
    public int IncreasedDamage
    {
        get => _increasedDamage;
        private set
        {
            AssertMutable();
            _increasedDamage = Math.Max(0, value);
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(CurrentDamage, ValueProp.Move)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Fatal)
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: IsFatalPreviewVisible
            ? FatalPortraitPath
            : NormalPortraitPath);

    public override IEnumerable<string> AllPortraitPaths =>
    [
        NormalPortraitPath,
        FatalPortraitPath
    ];

    // Manimani reserves the native red warning channel for its fully satisfied
    // Fatal condition. Its ordinary inherited gold rules remain untouched, but
    // the lethal/history hint is no longer added to the gold channel.
    protected override bool ShouldGlowGoldInternal =>
        base.ShouldGlowGoldInternal;

    protected override bool ShouldGlowRedInternal =>
        base.ShouldGlowRedInternal || HasSatisfiedFatalTarget;

    private bool HasSatisfiedFatalTarget =>
        CombatState is not null &&
        CombatState.HittableEnemies.Any(IsFatalTarget);

    public Manimani() : base(
        2,
        CardType.Attack,
        CardRarity.Rare,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        Creature target = cardPlay.Target;
        UpdateDynamicVarPreview(
            CardPreviewMode.Normal,
            target,
            DynamicVars);
        int rememberedDamage = GetDamageDealtByTargetThisCombat(target);
        bool fatalConditionSatisfied = IsFatalConditionSatisfied(
            target,
            DynamicVars.Damage.PreviewValue,
            rememberedDamage);

        // The full-screen m1-m4/m6 sequence belongs only to a fully satisfied
        // Fatal target. Ordinary plays skip the image sequence but retain the
        // target-local impact below.
        if (fatalConditionSatisfied)
            await MgrManimaniVfx.PlayPrelude(fatalConditionSatisfied);
        MgrManimaniVfx.SpawnImpact(target, fatalConditionSatisfied);

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx(null)
            .Execute(choiceContext);

        bool wasKilled = attack.Results
            .SelectMany(static results => results)
            .Any(static result => result.WasTargetKilled);
        if (!fatalConditionSatisfied || !wasKilled)
            return;

        IncreaseDamagePermanently(rememberedDamage);
        if (DeckVersion is Manimani deckVersion &&
            !ReferenceEquals(deckVersion, this))
        {
            deckVersion.IncreaseDamagePermanently(rememberedDamage);
        }
    }

    protected override void OnUpgrade()
    {
        UpdateDamage();
    }

    protected override void AfterDowngraded() => UpdateDamage();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("FatalPreview", IsFatalPreviewVisible);
    }

    /// <summary>
    /// Called from the NCard target-preview patch after Tower 2 has calculated
    /// target-specific damage. Returns true only when a second visual refresh is
    /// required for the alternate portrait/text.
    /// </summary>
    internal bool SetFatalPreview(Creature? target)
    {
        bool next = CombatManager.Instance.IsInProgress &&
            CombatState is not null &&
            Pile?.Type is PileType.Hand or PileType.Play &&
            target is not null &&
            IsFatalTargetFromPreview(target);
        if (_showFatalPreview == next)
            return false;

        _showFatalPreview = next;
        return true;
    }

    private bool IsFatalPreviewVisible =>
        _showFatalPreview &&
        CombatManager.Instance.IsInProgress &&
        CombatState is not null &&
        Pile?.Type is PileType.Hand or PileType.Play;

    /// <summary>
    /// Rendering-only state used by the description patch. The card keeps its
    /// real Exhaust keyword so result-pile routing and hover rules remain valid.
    /// </summary>
    internal bool IsFatalPreviewActive => IsFatalPreviewVisible;

    private bool IsFatalTarget(Creature target)
    {
        decimal damage = DynamicVars.Damage.BaseValue;
        if (Pile?.Type is PileType.Hand or PileType.Play)
        {
            UpdateDynamicVarPreview(
                CardPreviewMode.Normal,
                target,
                DynamicVars);
            damage = DynamicVars.Damage.PreviewValue;
        }

        return IsFatalConditionSatisfied(
            target,
            damage,
            GetDamageDealtByTargetThisCombat(target));
    }

    private bool IsFatalTargetFromPreview(Creature target) =>
        IsFatalConditionSatisfied(
            target,
            DynamicVars.Damage.PreviewValue,
            GetDamageDealtByTargetThisCombat(target));

    private static bool IsFatalConditionSatisfied(
        Creature target,
        decimal projectedDamage,
        int rememberedDamage) =>
        rememberedDamage > 0 &&
        CanTriggerFatal(target) &&
        projectedDamage >= target.CurrentHp + target.Block;

    private static bool CanTriggerFatal(Creature target) =>
        target.IsAlive &&
        target.IsEnemy &&
        target.Powers.All(static power => power.ShouldOwnerDeathTriggerFatal());

    /// <summary>
    /// Reads the combat history directly so the permanent-growth effect and
    /// its target preview share exactly the same source of truth. Any damage
    /// dealt by this enemy that actually reached this player's HP counts;
    /// fully blocked hits are excluded by UnblockedDamage.
    /// </summary>
    private int GetDamageDealtByTargetThisCombat(Creature target)
    {
        return CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Where(entry =>
                ReferenceEquals(entry.Receiver, Owner.Creature) &&
                ReferenceEquals(entry.Dealer, target) &&
                entry.Result.UnblockedDamage > 0)
            .Sum(static entry => entry.Result.UnblockedDamage);
    }

    private void IncreaseDamagePermanently(int amount)
    {
        if (amount <= 0)
            return;

        IncreasedDamage += amount;
        UpdateDamage();
    }

    private void UpdateDamage()
    {
        CurrentDamage = checked(
            BaseDamage +
            (IsUpgraded ? UpgradedDamageBonus : 0) +
            IncreasedDamage);
    }
}
