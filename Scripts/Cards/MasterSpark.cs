using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "master_spark")]
public sealed class MasterSpark : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(26m, ValueProp.Move),
        new IntVar("ForteLoss", 1m)
    ];

    public MasterSpark() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            // vfx_star_attack does not exist in STS2 v0.108.0. A missing hit
            // scene throws from VfxCmd and leaves the manual-play card centered.
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner.Creature,
            -DynamicVars["ForteLoss"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
    }
}
