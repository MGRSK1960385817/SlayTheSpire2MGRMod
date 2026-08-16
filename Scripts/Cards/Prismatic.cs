using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "prismatic")]
public sealed class Prismatic : MgrCard
{
    public Prismatic() : base(
        3,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay) => PowerCmd.Apply<PrismaticPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
