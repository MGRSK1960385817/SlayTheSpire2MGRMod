using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "heat_abnormal")]
public sealed class HeatAbnormal : MgrCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        MgrHoverTips.BaseDamage()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new IntVar("Performance", 1m)
    ];

    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseStart;

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    public HeatAbnormal() : base(
        1,
        CardType.Attack,
        CardRarity.Rare,
        TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        bool isStarting = IsPhraseStart;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        if (isStarting)
        {
            MgrCombatCardMutationState.Increase(
                this,
                "Damage",
                DynamicVars.Damage.BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(1m);
    }
}
