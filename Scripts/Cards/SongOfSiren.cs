using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "song_of_siren")]
public sealed class SongOfSiren : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new PowerVar<StrengthPower>(1m),
        new IntVar("Performance", 2m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    public SongOfSiren() : base(
        2,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await MgrSongOfSirenVfx.Play(this);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (CombatState is not { } combatState)
            return;

        foreach (var enemy in combatState.HittableEnemies)
        {
            // The Siren wave belongs on its recipients. A previous player-side
            // flying slash and generic cast burst made the wave appear to fire
            // inward at MGR instead of travelling out toward the enemies.
            MgrAbilityVfx.SpawnCastBurst(
                enemy,
                MgrAbilityVfxStyle.Siren,
                0.52f);
        }
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            combatState.HittableEnemies,
            -DynamicVars["StrengthPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() =>
        DynamicVars.Block.UpgradeValueBy(2m);
}
