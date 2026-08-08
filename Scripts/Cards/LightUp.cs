using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "light_up")]
public sealed class LightUp : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseStart;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new IntVar("Hits", 1m),
        new IntVar("Performance", 2m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public LightUp() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool isStarting = IsPhraseStart;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (isStarting)
            MgrCombatCardMutationState.Increase(this, "Hits", 1m);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(1m);
    }
}
