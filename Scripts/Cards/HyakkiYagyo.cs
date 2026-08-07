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

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "hyakki_yagyo")]
public sealed class HyakkiYagyo : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new ExtraDamageVar(1m),
        new CalculationExtraVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(
            static (card, _) => PileType.Exhaust.GetPile(card.Owner).Cards.Count),
        new CalculatedVar("CalculatedHits").WithMultiplier(
            static (card, _) =>
                1m + MgrCurseUtils.CountCurses(card.Owner))
    ];

    public HyakkiYagyo() : base(
        2,
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

        if (IsUpgraded)
        {
            for (int index = 0; index < 2; index++)
            {
                await MgrCurseUtils.AddRandomCurseToCombat(
                    Owner,
                    PileType.Discard);
            }
        }

        int hits = (int)((CalculatedVar)DynamicVars["CalculatedHits"])
            .Calculate(null);
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
    }
}
