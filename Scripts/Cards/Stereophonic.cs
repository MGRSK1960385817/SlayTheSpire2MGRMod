using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "stereophonic")]
public sealed class Stereophonic : MgrCard
{
    public Stereophonic() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        StereophonicPower? basePower = Owner.Creature.Powers
            .OfType<StereophonicPower>()
            .Where(power => power.GetType() == typeof(StereophonicPower))
            .FirstOrDefault();
        StereophonicPlusPower? plusPower = Owner.Creature.Powers
            .OfType<StereophonicPlusPower>()
            .FirstOrDefault();

        if (!IsUpgraded)
        {
            if (basePower is null && plusPower is null)
            {
                await PowerCmd.Apply<StereophonicPower>(
                    choiceContext,
                    Owner.Creature,
                    1m,
                    Owner.Creature,
                    this);
            }

            return;
        }

        if (basePower is not null)
            await PowerCmd.Remove(basePower);
        if (plusPower is null)
        {
            await PowerCmd.Apply<StereophonicPlusPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
