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
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        await ChannelNote(choiceContext, NoteKind.Attack);
        await ChannelNote(choiceContext, NoteKind.Skill);
        await ChannelNote(choiceContext, NoteKind.Power);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}
