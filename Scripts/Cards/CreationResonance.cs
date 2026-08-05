using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "creation_resonance")]
public sealed class CreationResonance : MgrCard
{
    public CreationResonance() : base(
        1,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<CreationResonancePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
