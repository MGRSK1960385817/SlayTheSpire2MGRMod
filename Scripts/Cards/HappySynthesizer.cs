using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "happy_synthesizer")]
public sealed class HappySynthesizer : MgrCard
{
    public HappySynthesizer() : base(
        2,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay) => PowerCmd.Apply<HappySynthesizerPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
