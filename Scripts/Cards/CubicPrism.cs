using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "cubic_prism")]
public sealed class CubicPrism : MgrCard
{
    private int _performanceX;

    protected override bool HasEnergyCostX => true;
    public override int InitialPerformanceTurns =>
        checked(_performanceX + (IsUpgraded ? 1 : 0));

    internal override int GetPerformanceTurnsForResultRouting(ResourceInfo resources) =>
        checked(
            Math.Max(_performanceX, resources.EnergySpent) +
            (IsUpgraded ? 1 : 0) +
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

        if (_performanceX <= 0 || combatState.HittableEnemies.Count == 0)
            return;

        await DamageCmd.Attack(_performanceX)
            .WithHitCount(_performanceX)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
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
