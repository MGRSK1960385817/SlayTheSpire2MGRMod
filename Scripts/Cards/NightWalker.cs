using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "night_walker")]
public sealed class NightWalker : MgrCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        MgrHoverTips.CardsInCombat()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new DamageVar(6m, ValueProp.Move),
        new CalculatedVar("TotalHits").WithMultiplier(
            static (card, _) =>
            {
                if (card.CombatState is null)
                    return 0m;

                // Night Walker creates its curse before resolving damage, so
                // the combat preview includes that guaranteed new curse.
                return MgrCurseUtils.CountCurses(card.Owner) + 1m;
            })
    ];

    public NightWalker() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        await MgrCurseUtils.AddRandomCurseToCombat(Owner, PileType.Discard);
        int curseCount = MgrCurseUtils.CountCurses(Owner);
        if (curseCount <= 0 || combatState.HittableEnemies.Count == 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(combatState)
            .WithHitCount(curseCount)
            .WithHitVfxNode(target => MgrAttackVfx.CreateGaseousImpact(
                target,
                MgrAttackVfx.CursePurple,
                0.9f))
            .WithHitFx(null, null, "blunt_attack.mp3")
            .OnlyPlayAnimOnce()
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
