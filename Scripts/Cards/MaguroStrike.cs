using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "maguro_strike")]
public sealed class MaguroStrike : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.ChordResolvedThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new IntVar("Notes", 1m)
    ];

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    public MaguroStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool chordWasAlreadyPlayed = NoteState.ChordsResolvedThisTurn > 0;

        int repetitions = chordWasAlreadyPlayed ? 2 : 1;
        for (int index = 0; index < repetitions; index++)
        {
            MgrAttackVfx.SpawnFishRush(
                Owner.Creature,
                cardPlay.Target,
                0.62f);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx(VfxCmd.slashPath)
                .Execute(choiceContext);
            for (int noteIndex = 0; noteIndex < DynamicVars["Notes"].IntValue; noteIndex++)
                await ChannelNote(choiceContext, NoteKind.Attack);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(1m);
    }
}
