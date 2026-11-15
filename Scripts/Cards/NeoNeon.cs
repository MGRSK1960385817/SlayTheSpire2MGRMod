using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "neo_neon")]
public sealed class NeoNeon : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 1m),
        new IntVar("Damage", 0m) // Used by the previous descriptions pending translation.
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public NeoNeon() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MgrSignatureVfx.SpawnRainbowStarRing(Owner.Creature, 26);
        await ChannelNote(choiceContext, NoteKind.Skill);
        await ChannelNote(choiceContext, NoteKind.Attack);
        await ChannelNote(choiceContext, NoteKind.Power);
    }

    protected override void OnUpgrade() =>
        DynamicVars["Performance"].UpgradeValueBy(1m);
}
