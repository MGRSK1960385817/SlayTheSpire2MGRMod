using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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

    private int _performanceX;

    internal int LockedPerformanceX => _performanceX;
    internal int LockedDamageAndHits =>
        checked(_performanceX + (IsUpgraded ? 1 : 0));

    protected override bool HasEnergyCostX => true;
    public override int InitialPerformanceTurns => _performanceX;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(0m, ValueProp.Move)
    ];

    internal override int GetPerformanceTurnsForResultRouting(ResourceInfo resources) =>
        checked(
            Math.Max(_performanceX, resources.EnergySpent) +
            MgrPerformanceModifierState.GetAdditionalPerformances(this));

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

        bool isQueued = MgrPerformanceSystem.IsQueued(this);
        string damage = isQueued
            ? DynamicVars.Damage.ToHighlightedString(inverse: false)
            : "X";
        string hits = isQueued
            ? LockedDamageAndHits.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : "X";

        // Both branches use the current locked values while queued; outside
        // combat the IfUpgraded formatter selects the original X or X+1 text.
        description.Add(BaseDamageDisplay, damage);
        description.Add(
            UpgradedDamageDisplay,
            isQueued ? damage : "X+1");
        description.Add(BaseHitsDisplay, hits);
        description.Add(
            UpgradedHitsDisplay,
            isQueued ? hits : "X+1");
    }

    private void SyncLockedDamageVar() =>
        DynamicVars.Damage.BaseValue = LockedDamageAndHits;

    protected override void OnUpgrade()
    {
        if (_performanceX > 0)
            SyncLockedDamageVar();
    }
}
