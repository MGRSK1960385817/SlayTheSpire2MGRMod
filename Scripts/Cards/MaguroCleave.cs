using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Godot;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_cleave")]
public sealed class MaguroCleave : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.ChordResolvedThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new IntVar("BonusPerChord", 3m)
    ];

    public MaguroCleave() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        decimal damage = DynamicVars.Damage.BaseValue +
            DynamicVars["BonusPerChord"].BaseValue * NoteState.ChordsResolvedThisTurn;
        float vfxScale = MgrAttackVfx.ScaleByDamage(
            damage,
            DynamicVars.Damage.BaseValue,
            baseScale: 0.9f,
            growthPerDoubling: 0.35f,
            maxScale: 1.75f);

        await DamageCmd.Attack(damage)
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
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
