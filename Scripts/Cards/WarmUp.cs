using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "warm_up")]
public sealed class WarmUp : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FortePower>(1m),
        new IntVar("Performance", 2m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public WarmUp() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsFirstInSeries &&
            !MgrPerformanceSystem.IsResolvingPerformance(this))
        {
            MgrBlueCardVfx.SpawnWarmUp(Owner.Creature, completed: false);
        }

        return Task.CompletedTask;
    }

    public override async Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context)
    {
        MgrBlueCardVfx.SpawnWarmUp(context.Player.Creature, completed: true);
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            context.Player.Creature,
            DynamicVars["FortePower"].BaseValue,
            context.Player.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(-1m);
    }
}
