using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "rainbow_scale")]
public sealed class RainbowScale : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new BlockVar(3m, ValueProp.Move),
        new CardsVar(1),
        new CalculatedVar("TotalRepetitions").WithMultiplier(
            static (card, _) =>
            {
                if (card.CombatState is null ||
                    !MgrCombatStateStore.TryGet(card.Owner, out MgrCombatState state))
                {
                    return 0m;
                }

                int kinds = state.Phrase.Notes
                    .Select(note => note.Kind)
                    .Distinct()
                    .Count();
                return 1m + kinds;
            })
    ];

    public RainbowScale() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int kinds = NoteState.Phrase.Notes
            .Select(note => note.Kind)
            .Distinct()
            .Count();
        int repetitions = 1 + kinds;
        for (int index = 0; index < repetitions; index++)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}
