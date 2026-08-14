using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_reversal")]
public sealed class MaguroReversal : MgrCard
{
    public override bool GainsBlock => true;
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.NoChordResolvedThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new BlockVar(8m, ValueProp.Move)
    ];

    public MaguroReversal() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int repeats = NoteState.ChordsResolvedThisTurn == 0 ? 2 : 1;
        for (int index = 0; index < repeats; index++)
        {
            MgrAttackVfx.SpawnFishRush(
                Owner.Creature,
                cardPlay.Target,
                0.76f);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitVfxNode(target => MgrAttackVfx.CreateBigSlash(
                    target,
                    MgrAttackVfx.StarPurple,
                    0.8f))
                .WithHitVfxNode(target => MgrAttackVfx.CreateBigSlashImpact(
                    target,
                    MgrAttackVfx.StarGold,
                    0.72f,
                    index % 2 == 0 ? 48f : -48f))
                .WithHitFx(null, null, "slash_attack.mp3")
                .Execute(choiceContext);
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
