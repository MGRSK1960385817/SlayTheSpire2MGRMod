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

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "meteor_shower")]
public sealed class MeteorShower : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new IntVar("Hits", 6m)
    ];

    public override bool IsStarryCard => true;

    public MeteorShower() : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) ||
            !MgrCombatStateStore.TryGet(Owner, out MgrCombatState state))
        {
            return false;
        }

        int starryNotesGenerated = state.StarryNotesGeneratedThisTurn;
        if (starryNotesGenerated == 0)
            return false;

        modifiedCost = Math.Max(0m, originalCost - starryNotesGenerated);
        return true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .WithHitVfxNode(target => MgrAttackVfx.CreateStarryImpact(
                target,
                MgrAttackVfx.StarPurple,
                0.9f))
            .WithHitFx(null, null, "blunt_attack.mp3")
            .OnlyPlayAnimOnce()
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(2m);
    }
}
