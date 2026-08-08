using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "electric_angel")]
public sealed class ElectricAngel : MgrCard
{
    public ElectricAngel() : base(
        1,
        CardType.Power,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<ElectricAngelPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
