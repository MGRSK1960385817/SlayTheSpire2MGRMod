using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "heat_abnormal")]
public sealed class HeatAbnormal : MgrCard
{
    private sealed class PhraseStartDamageVar(decimal damage, ValueProp props)
        : DamageVar(damage, props)
    {
        public override void UpdateCardPreview(
            CardModel card,
            CardPreviewMode previewMode,
            Creature? target,
            bool runGlobalHooks)
        {
            base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
            if (card is not HeatAbnormal heatAbnormal ||
                !heatAbnormal.IsPhraseStartPreviewActive)
            {
                return;
            }

            // Preview the same new BaseValue that OnPlay will establish. Feed
            // that prospective base through the native damage pipeline so
            // Sharp is doubled with the card while Strength, Weak, Vigor and
            // target modifiers are still applied afterward exactly once.
            decimal prospectiveBase = BaseValue + heatAbnormal.GetIntrinsicDamage();
            if (runGlobalHooks)
            {
                PreviewValue = Hook.ModifyDamage(
                    card.Owner.RunState,
                    card.CombatState,
                    target,
                    card.Owner.Creature,
                    prospectiveBase,
                    Props,
                    card
#if !STS2_V107
                    ,
                    cardPlay: null,
#else
                    ,
#endif
                    ModifyDamageHookType.All,
                    previewMode,
                    out IEnumerable<AbstractModel> _);
            }
            else
            {
                EnchantmentModel? enchantment = card.Enchantment;
                if (enchantment is not null)
                {
                    prospectiveBase += enchantment.EnchantDamageAdditive(
                        prospectiveBase,
                        Props);
                    prospectiveBase *= enchantment.EnchantDamageMultiplicative(
                        prospectiveBase,
                        Props);
                }

                PreviewValue = prospectiveBase;
            }

            PreviewValue = Math.Max(PreviewValue, 0m);
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        MgrHoverTips.BaseDamage()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PhraseStartDamageVar(2m, ValueProp.Move),
        new IntVar("Performance", 2m)
    ];

    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseStart;

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    public HeatAbnormal() : base(
        1,
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

        bool isStarting = IsPhraseStart;
        if (isStarting)
        {
            MgrCombatCardMutationState.Increase(
                this,
                "Damage",
                GetIntrinsicDamage());
        }

        decimal intrinsicDamage = GetIntrinsicDamage();
        float vfxScale = MgrAttackVfx.ScaleByDamage(
            intrinsicDamage,
            referenceDamage: 3m,
            baseScale: 0.6f,
            growthPerDoubling: 0.18f,
            maxScale: 1.35f);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitVfxNode(target => MgrAttackVfx.CreateFireBurst(
                target,
                MgrAttackVfx.DefaultFireTint,
                vfxScale))
            .WithHitFx(null, null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    /// <summary>
    /// Heat Abnormal treats Sharp as part of the damage printed on the card.
    /// Strength, Vigor, Vigorous and other combat-time modifiers remain outside
    /// this value and are therefore applied only after its base damage doubles.
    /// </summary>
    private decimal GetIntrinsicDamage()
    {
        decimal damage = DynamicVars.Damage.BaseValue;
        if (Enchantment is Sharp sharp)
        {
            damage += sharp.EnchantDamageAdditive(
                damage,
                DynamicVars.Damage.Props);
        }

        return damage;
    }

    private bool IsPhraseStartPreviewActive =>
        CombatState is not null &&
        IsPhraseStart;

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}
