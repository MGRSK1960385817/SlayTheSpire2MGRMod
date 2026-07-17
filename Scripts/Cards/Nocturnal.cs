using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "nocturnal")]
public sealed class Nocturnal : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    public Nocturnal() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        await MgrCurseUtils.AddRandomCurseToCombat(Owner, PileType.Draw);
        int curseCount = MgrCurseUtils.CountCurses(Owner);
        if (curseCount <= 0 || combatState.HittableEnemies.Count == 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(combatState)
            .WithHitCount(curseCount)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
