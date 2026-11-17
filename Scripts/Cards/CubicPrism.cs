using System.Globalization;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "cubic_prism")]
public sealed class CubicPrism : MgrCard
{
    private const string BaseDamageDisplay = "CubicPrismBaseDamage";
    private const string UpgradedDamageDisplay = "CubicPrismUpgradedDamage";
    private const string BaseHitsDisplay = "CubicPrismBaseHits";
    private const string UpgradedHitsDisplay = "CubicPrismUpgradedHits";

    private sealed class PrismDamageVar() : DamageVar(0m, ValueProp.Move)
    {
        public int? PreviewHits { get; private set; }

        public override void UpdateCardPreview(
            CardModel card,
            CardPreviewMode previewMode,
            Creature? target,
            bool runGlobalHooks)
        {
            var prism = (CubicPrism)card;
            PreviewHits = null;
            if (MgrPerformanceSystem.IsQueued(prism))
            {
                PreviewHits = prism.LockedDamageAndHits;
            }
            else if (runGlobalHooks && prism.GetCurrentEnergyXForPreview() is { } x)
            {
                PreviewHits = checked(x + (prism.IsUpgraded || previewMode == CardPreviewMode.Upgrade ? 1 : 0));
            }

            if (PreviewHits is not { } hits)
            {
                base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
                return;
            }

            if (hits <= 0)
            {
                PreviewValue = EnchantedValue = 0m;
                return;
            }

            // Previewing a different X must leave the damage locked by play intact.
            var preview = new DamageVar(hits, Props);
            preview.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
            EnchantedValue = preview.EnchantedValue;
            PreviewValue = preview.PreviewValue;
        }
    }

    private int _performanceX;
    private bool UsesCurrentEnergyPreview =>
        Pile?.Type == PileType.Hand || UpgradePreviewType == CardUpgradePreviewType.Combat;

    internal int LockedDamageAndHits =>
        checked(_performanceX + (IsUpgraded ? 1 : 0));

    protected override bool HasEnergyCostX => true;
    public override int InitialPerformanceTurns => _performanceX;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PrismDamageVar()
    ];

    internal int? GetCurrentEnergyXForPreview() =>
        UsesCurrentEnergyPreview && CombatState is { } combatState
            ? Hook.ModifyXValue(combatState, this, EnergyCost.GetAmountToSpend())
            : null;

    internal override int GetPerformanceTurnsForResultRouting(bool isAutoPlay, ResourceInfo resources) =>
        MgrPerformanceSystem.ApplyPerformanceFloor(
            this, isAutoPlay ? _performanceX : ResolveEnergyXValue());

    public CubicPrism() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        if (!cardPlay.IsAutoPlay)
        {
            _performanceX = ResolveEnergyXValue();
            SyncLockedDamageVar();
        }

        int damageAndHits = LockedDamageAndHits;
        if (damageAndHits <= 0 || combatState.HittableEnemies.Count == 0)
            return;

        float beamScale = Math.Clamp(
            0.24f + MathF.Sqrt(damageAndHits) * 0.25f,
            0.32f,
            1.75f);
        MgrRegentStructureVfx.SpawnCubicPrismRefraction(
            Owner.Creature,
            combatState.HittableEnemies,
            beamScale);
        await MgrAttackVfx.PlaySweepingBeam(
            this,
            Owner.Creature,
            combatState.HittableEnemies.ToList(),
            MgrAttackVfx.StarPurple,
            beamScale);
        await DamageCmd.Attack(damageAndHits)
            .WithHitCount(damageAndHits)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .OnlyPlayAnimOnce()
            .Execute(choiceContext);
    }

    public override Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context)
    {
        _performanceX = 0;
        SyncLockedDamageVar();
        return Task.CompletedTask;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);

        var damageVar = (PrismDamageVar)DynamicVars.Damage;
        int? previewHits = MgrPerformanceSystem.IsQueued(this)
            ? LockedDamageAndHits
            : UsesCurrentEnergyPreview ? damageVar.PreviewHits : null;
        if (previewHits is { } hits)
        {
            string damage = damageVar.ToHighlightedString(inverse: false);
            string hitCount = Math.Max(hits, 0).ToString(CultureInfo.InvariantCulture);
            description.Add(BaseDamageDisplay, damage);
            description.Add(UpgradedDamageDisplay, damage);
            description.Add(BaseHitsDisplay, hitCount);
            description.Add(UpgradedHitsDisplay, hitCount);
            return;
        }

        description.Add(BaseDamageDisplay, FormatDamageFormula(0));
        description.Add(UpgradedDamageDisplay, FormatDamageFormula(1));
        description.Add(BaseHitsDisplay, "X");
        description.Add(UpgradedHitsDisplay, "X+1");
    }

    private string FormatDamageFormula(int upgradeBonus)
    {
        if (Enchantment is Instinct)
            return upgradeBonus == 0 ? "2×X" : "2×(X+1)";

        int bonus = upgradeBonus + (Enchantment is Sharp sharp ? sharp.Amount : 0);
        return bonus == 0 ? "X" : $"X+{bonus.ToString(CultureInfo.InvariantCulture)}";
    }

    private void SyncLockedDamageVar() =>
        DynamicVars.Damage.BaseValue = LockedDamageAndHits;

    protected override void OnUpgrade()
    {
        if (_performanceX > 0)
            SyncLockedDamageVar();
    }
}
