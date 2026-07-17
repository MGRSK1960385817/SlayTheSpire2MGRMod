using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "the_cursed")]
public sealed class TheCursed : MgrCard
{
    public TheCursed() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TheCursedPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        await MgrCurseUtils.AddRandomCurseToCombat(Owner, PileType.Draw);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
