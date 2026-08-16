using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Godot;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_cleave")]
public sealed class MaguroCleave : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.ChordResolvedThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(6m),
        new ExtraDamageVar(3m),
        new CalculatedDamageVar(ValueProp.Move)
            .WithMultiplier(static (card, _) =>
                card is MaguroCleave cleave
                    ? cleave.NoteState.ChordsResolvedThisTurn
                    : 0m)
    ];

    public MaguroCleave() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        decimal damage = DynamicVars.CalculatedDamage.Calculate(null);
        float vfxScale = MgrAttackVfx.ScaleByDamage(
            damage,
            DynamicVars.CalculationBase.BaseValue,
            baseScale: 0.9f,
            growthPerDoubling: 0.35f,
            maxScale: 1.75f);

        foreach (var target in combatState.HittableEnemies)
            MgrAttackVfx.SpawnFishRush(Owner.Creature, target, vfxScale * 0.78f);

        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitVfxNode(target => MgrAttackVfx.CreateHorizontalSlash(
                target,
                Colors.White,
                vfxScale))
            .WithHitFx(null, null, "slash_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(3m);
    }
}
