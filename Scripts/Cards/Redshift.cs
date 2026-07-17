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

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "redshift")]
public sealed class Redshift : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(16m, ValueProp.Move)
    ];

    public override bool IsStarryCard => true;

    public Redshift() : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this))
            return false;

        int reduction = MgrCombatStateStore.For(Owner).UnusedEnergyLastTurn;
        if (reduction <= 0)
            return false;

        modifiedCost = Math.Max(0m, originalCost - reduction);
        return true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
    }
}
