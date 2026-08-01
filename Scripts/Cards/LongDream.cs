using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "long_dream")]
public sealed class LongDream : MgrCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("StrengthLoss", 4m),
        new IntVar("Performance", 1m)
    ];

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    public LongDream() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        var targets = combatState.Creatures
            .Where(creature => creature.IsAlive)
            .ToArray();
        decimal amount = DynamicVars["StrengthLoss"].BaseValue;
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            targets,
            -amount,
            Owner.Creature,
            this);
        await PowerCmd.Apply<LongDreamPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
        Owner.Creature.Powers.OfType<LongDreamPower>()
            .FirstOrDefault()
            ?.RecordLoss(targets, amount);

    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthLoss"].UpgradeValueBy(2m);
    }
}
