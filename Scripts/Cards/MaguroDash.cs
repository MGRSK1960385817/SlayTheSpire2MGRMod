using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_dash")]
public sealed class MaguroDash : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("TotalHits").WithMultiplier(
            static (card, _) =>
                card.CombatState is null
                    ? 0m
                    : 1m + MgrPerformanceSystem.GetQueuedCards(card.Owner).Count)
    ];

    public MaguroDash() : base(
        1,
        CardType.Attack,
        CardRarity.Uncommon,
        TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AttackAll(choiceContext, cardPlay);
        await MgrPerformanceSystem.EndAllPerformancesWithFinisher(
            choiceContext,
            Owner,
            this,
            _ => AttackAll(choiceContext, cardPlay));
    }

    private async Task AttackAll(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        foreach (var target in Owner.Creature.CombatState!.HittableEnemies)
            MgrAttackVfx.SpawnFishRush(Owner.Creature, target, 0.92f);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .WithHitVfxNode(target => MgrAttackVfx.CreateHorizontalSlash(
                target,
                Colors.White,
                1.05f))
            .WithHitFx(null, null, "slash_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
