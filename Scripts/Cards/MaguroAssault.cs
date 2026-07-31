using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_assault")]
public sealed class MaguroAssault : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseEnd;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(6m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move)
            .WithMultiplier(static (card, _) =>
            {
                if (card is not MaguroAssault assault)
                    return 0m;

                decimal chords = assault.NoteState.ChordsResolvedThisCombat;
                if (!assault.IsPhraseEndBonusActive)
                    return chords;

                // CalculatedDamage = base + extra * multiplier. Folding the
                // Ending bonus into that native formula makes both preview and
                // AttackCommand calculate the same doubled raw value, after
                // which Strength, enchantments and target hooks run normally.
                decimal extra = assault.DynamicVars.ExtraDamage.BaseValue;
                return extra == 0m
                    ? chords
                    : chords * 2m +
                        assault.DynamicVars.CalculationBase.BaseValue / extra;
            })
    ];

    public MaguroAssault() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
        DynamicVars.ExtraDamage.UpgradeValueBy(1m);
    }
}
