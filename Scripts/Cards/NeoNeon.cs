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

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "neo_neon")]
public sealed class NeoNeon : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new IntVar("Performance", 1m)
    ];

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    public NeoNeon() : base(
        0,
        CardType.Attack,
        CardRarity.Uncommon,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitVfxNode(target =>
            {
                MgrSignatureVfx.SpawnRainbowStarRing(target, 26);
                return MgrAttackVfx.CreateGunshot(
                    Owner.Creature,
                    target,
                    MgrAttackVfx.StarGold,
                    1f);
            })
            .WithHitFx(null, null, "blunt_attack.mp3")
            .Execute(choiceContext);

        await ChannelNote(choiceContext, NoteKind.Attack);
        await ChannelNote(choiceContext, NoteKind.Skill);
        await ChannelNote(choiceContext, NoteKind.Power);
    }

    protected override void OnUpgrade() =>
        DynamicVars["Performance"].UpgradeValueBy(1m);
}
