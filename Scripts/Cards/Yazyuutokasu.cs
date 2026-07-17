using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "yazyuutokasu")]
public sealed class Yazyuutokasu : MgrCard
{
    public Yazyuutokasu() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<YazyuutokasuPlusPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }
        else
        {
            await PowerCmd.Apply<YazyuutokasuPower>(
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
