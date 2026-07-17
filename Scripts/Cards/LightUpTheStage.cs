using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "light_up_the_stage")]
public sealed class LightUpTheStage : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new IntVar("Hits", 3m),
        new IntVar("DamageGrowth", 3m),
        new IntVar("DiscardHitGrowth", 2m)
    ];

    public LightUpTheStage() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        MgrCombatCardMutationState.Increase(
            this,
            "Damage",
            DynamicVars["DamageGrowth"].BaseValue);
    }

    public override Task AfterCardDiscarded(
        PlayerChoiceContext choiceContext,
        CardModel card)
    {
        if (ReferenceEquals(card, this))
        {
            MgrCombatCardMutationState.Increase(
                this,
                "Hits",
                DynamicVars["DiscardHitGrowth"].BaseValue);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m);
    }
}
