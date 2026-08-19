using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_bash")]
public sealed class MaguroBash : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.ChordTriggeredThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(20m, ValueProp.Move)
    ];

    public MaguroBash() : base(4, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) ||
            !MgrCombatStateStore.TryGet(Owner, out MgrCombatState state) ||
            state.ChordTriggersThisTurn == 0)
        {
            return false;
        }

        modifiedCost = Math.Max(
            0m,
            originalCost - state.ChordTriggersThisTurn);
        return true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        MgrAttackVfx.SpawnFishRush(
            Owner.Creature,
            cardPlay.Target,
            1.52f);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitVfxNode(target => MgrAttackVfx.CreateBigSlash(
                target,
                MgrAttackVfx.StarPurple,
                1.18f))
            .WithHitVfxNode(target => MgrAttackVfx.CreateBigSlashImpact(
                target,
                MgrAttackVfx.StarGold,
                1.08f))
            .WithHitFx(null, null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7m);
    }
}
