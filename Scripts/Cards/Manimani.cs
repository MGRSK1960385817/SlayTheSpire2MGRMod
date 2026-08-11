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
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Cards;

/// <summary>
/// A Fatal attack which permanently remembers the unblocked Move damage dealt
/// by its victim during the current combat. Its combat portrait and rules text
/// change only while the player is actively targeting a valid lethal victim.
/// </summary>
[RegisterCard(typeof(MgrCardPool), StableEntryStem = "manimani")]
public sealed class Manimani : MgrCard
{
    private const int BaseDamage = 12;
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

    protected override bool ShouldGlowGoldInternal =>
        base.ShouldGlowGoldInternal ||
        CombatState is not null &&
        CombatState.HittableEnemies.Any(IsFatalTarget);

    public Manimani() : base(
        3,
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
        bool shouldTriggerFatal = CanTriggerFatal(target);
        int rememberedDamage = shouldTriggerFatal
            ? GetDamageDealtByTargetThisCombat(target)
            : 0;

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_starry_impact", null, "heavy_attack.mp3")
            .Execute(choiceContext);

        bool wasKilled = attack.Results
            .SelectMany(static results => results)
            .Any(static result => result.WasTargetKilled);
        if (!shouldTriggerFatal || !wasKilled || rememberedDamage <= 0)
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
        EnergyCost.UpgradeBy(-1);
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
        if (!CanTriggerFatal(target))
            return false;

        decimal damage = DynamicVars.Damage.BaseValue;
        if (Pile?.Type is PileType.Hand or PileType.Play)
        {
            UpdateDynamicVarPreview(
                CardPreviewMode.Normal,
                target,
                DynamicVars);
            damage = DynamicVars.Damage.PreviewValue;
        }

        return damage >= target.CurrentHp + target.Block;
    }

    private bool IsFatalTargetFromPreview(Creature target) =>
        CanTriggerFatal(target) &&
        DynamicVars.Damage.PreviewValue >= target.CurrentHp + target.Block;

    private static bool CanTriggerFatal(Creature target) =>
        target.IsAlive &&
        target.IsEnemy &&
        target.Powers.All(static power => power.ShouldOwnerDeathTriggerFatal());

    /// <summary>
    /// Reads the combat history directly so the permanent-growth effect does
    /// not depend on a separate listener having mirrored every damage event.
    /// Only powered Move damage that actually reached this player's HP counts;
    /// blocked damage and damage from powers/relics are therefore excluded.
    /// </summary>
    private int GetDamageDealtByTargetThisCombat(Creature target)
    {
        return CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Where(entry =>
                ReferenceEquals(entry.Receiver, Owner.Creature) &&
                ReferenceEquals(entry.Dealer, target) &&
                entry.Result.Props.IsPoweredAttack())
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
        CurrentDamage = checked(BaseDamage + IncreasedDamage);
    }
}
