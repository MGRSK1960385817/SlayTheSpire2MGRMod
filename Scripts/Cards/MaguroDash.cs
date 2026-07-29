using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_dash")]
public sealed class MaguroDash : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
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
        int ended = await MgrPerformanceSystem.EndAllPerformances(choiceContext, Owner);
        for (int index = 0; index < ended; index++)
            await AttackAll(choiceContext, cardPlay);
    }

    private Task AttackAll(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .Execute(choiceContext);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
