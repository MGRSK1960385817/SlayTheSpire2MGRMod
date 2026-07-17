using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "show_weakness")]
public sealed class ShowWeakness : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("StrengthLoss", 3m),
        new IntVar("Notes", 3m)
    ];

    public ShowWeakness() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
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
        await PowerCmd.Apply<ShowWeaknessPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
        Owner.Creature.Powers.OfType<ShowWeaknessPower>()
            .FirstOrDefault()
            ?.RecordLoss(targets, amount);

        for (int index = 0; index < DynamicVars["Notes"].IntValue; index++)
            await ChannelNote(choiceContext, NoteKind.Skill);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthLoss"].UpgradeValueBy(1m);
        DynamicVars["Notes"].UpgradeValueBy(1m);
    }
}
