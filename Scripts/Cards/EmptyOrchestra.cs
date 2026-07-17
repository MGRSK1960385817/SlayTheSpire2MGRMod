using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "empty_orchestra")]
public sealed class EmptyOrchestra : MgrCard
{
    public EmptyOrchestra() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int removed = MgrNoteSystem.RemoveAllNotes(Owner).Count;
        if (removed <= 0)
            return;

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            removed,
            Owner.Creature,
            this);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner.Creature,
            removed,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
