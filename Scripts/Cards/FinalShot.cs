using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "final_shot")]
public sealed class FinalShot : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseEnd;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // Keep both halves explicit: outside combat the CalculationBase still
        // renders the ordinary value, while Ending adds one matching ExtraDamage
        // term through Tower 2's native calculation path.
        new CalculationBaseVar(12m),
        new ExtraDamageVar(12m),
        new CalculatedDamageVar(ValueProp.Move)
            .WithMultiplier(static (card, _) =>
                card is FinalShot shot && shot.IsPhraseEndBonusActive ? 1m : 0m)
    ];

    public FinalShot() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(5m);
        DynamicVars.ExtraDamage.UpgradeValueBy(5m);
    }
}
