using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "bird")]
public sealed class Bird : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(24m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("CalculatedNotes").WithMultiplier(
            static (card, _) =>
                Math.Floor(card.DynamicVars.Damage.PreviewValue / 2m))
    ];

    public Bird() : base(
        3,
        CardType.Attack,
        CardRarity.Rare,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        int notesToGenerate = GetNotesToGenerate(cardPlay.Target, cardPlay);
        MgrSignatureVfx.SpawnFallingBird(cardPlay.Target, 1.22f);
        await MgrAttackVfx.PlayLargeMagicMissile(
            this,
            cardPlay.Target,
            MgrAttackVfx.StarGold,
            scale: 1.38f);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitVfxNode(target => MgrAttackVfx.CreateStarryImpact(
                target,
                MgrAttackVfx.StarGold,
                1.25f))
            .WithHitFx(null, null, "heavy_attack.mp3")
            .Execute(choiceContext);

        // Keep the Note cascade visually downstream of the falling bird and
        // impact. Without this beat the first Note entrance starts on the same
        // frame as the damage, swallowing the card's signature landing.
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(this, 0.20f));
        for (int index = 0; index < notesToGenerate; index++)
            await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, Owner);
    }

    private int GetNotesToGenerate(Creature target, CardPlay cardPlay)
    {
        decimal finalDamage = Hook.ModifyDamage(
            Owner.RunState,
            CombatState,
            target,
            Owner.Creature,
            DynamicVars.Damage.BaseValue,
            ValueProp.Move,
            this,
            cardPlay,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out IEnumerable<AbstractModel> _);
        return Math.Max(0, (int)Math.Floor(finalDamage / 2m));
    }

    protected override void OnUpgrade() =>
        DynamicVars.Damage.UpgradeValueBy(6m);
}
