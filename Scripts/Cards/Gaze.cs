using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "gaze")]
public sealed class Gaze : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CardsVar(1),
        new CalculatedVar("CalculatedDraw").WithMultiplier(
            static (_, target) => CountDistinctDebuffs(target))
    ];

    public Gaze() : base(
        1,
        CardType.Attack,
        CardRarity.Common,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int debuffCount = CountDistinctDebuffs(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(VfxCmd.gazePath)
            .Execute(choiceContext);

        if (debuffCount > 0)
            await CardPileCmd.Draw(choiceContext, debuffCount, Owner);
    }

    protected override void OnUpgrade() =>
        DynamicVars.Damage.UpgradeValueBy(3m);

    private static int CountDistinctDebuffs(Creature? target) =>
        target?.Powers
            .Where(power =>
                power.TypeForCurrentAmount == PowerType.Debuff &&
                power is not ITemporaryPower)
            .Select(power => power.Id)
            .Distinct()
            .Count() ?? 0;

}
