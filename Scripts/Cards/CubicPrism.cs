using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "cubic_prism")]
public sealed class CubicPrism : MgrCard
{
    private int _performanceX;

    protected override bool HasEnergyCostX => true;
    public override int InitialPerformanceTurns => _performanceX;

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
            _performanceX = ResolveEnergyXValue();

        int damageAndHits = checked(_performanceX + (IsUpgraded ? 1 : 0));
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
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
    }
}
