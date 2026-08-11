using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "gaze")]
public sealed class Gaze : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(3m),
        new DebuffConditionalBlockVar(ValueProp.Move).WithMultiplier(
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
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(VfxCmd.gazePath)
            .Execute(choiceContext);

        int debuffCount = CountDistinctDebuffs(cardPlay.Target);
        decimal blockPerDebuff = DynamicVars.CalculationExtra.BaseValue;
        for (int index = 0; index < debuffCount; index++)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                blockPerDebuff,
                DynamicVars.CalculatedBlock.Props,
                cardPlay,
                fast: index > 0);
        }
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

    /// <summary>
    /// Each distinct debuff represents a separate block gain. This makes every
    /// copy receive Dexterity and other block modifiers independently while a
    /// target with no debuffs still produces no block effect at all.
    /// </summary>
    private sealed class DebuffConditionalBlockVar(ValueProp props)
        : CalculatedBlockVar(props)
    {
        public override void UpdateCardPreview(
            CardModel card,
            CardPreviewMode previewMode,
            Creature? target,
            bool runGlobalHooks)
        {
            int debuffCount = CountDistinctDebuffs(target);
            if (debuffCount == 0)
            {
                EnchantedValue = 0m;
                PreviewValue = 0m;
                return;
            }

            decimal blockPerDebuff = GetExtraVar().BaseValue;
            decimal enchantedBlockPerDebuff = blockPerDebuff;
            if (card.Enchantment is { } enchantment)
            {
                enchantedBlockPerDebuff +=
                    enchantment.EnchantBlockAdditive(enchantedBlockPerDebuff);
                enchantedBlockPerDebuff *=
                    enchantment.EnchantBlockMultiplicative(enchantedBlockPerDebuff);
            }

            EnchantedValue = enchantedBlockPerDebuff * debuffCount;
            if (!runGlobalHooks || card.CombatState is not { } combatState)
            {
                PreviewValue = EnchantedValue;
                return;
            }

            decimal modifiedBlockPerDebuff = Hook.ModifyBlock(
                combatState,
                card.Owner.Creature,
                blockPerDebuff,
                Props,
                card,
                cardPlay: null,
                out _);
            PreviewValue = modifiedBlockPerDebuff * debuffCount;
        }
    }
}
