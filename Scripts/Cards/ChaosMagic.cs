using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "chaos_magic")]
public sealed class ChaosMagic : MgrCard
{
    public ChaosMagic() : base(
        2,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ChaosMagicPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (IsUpgraded)
            await MgrNoteSystem.CopyAllNotes(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
    }
}
