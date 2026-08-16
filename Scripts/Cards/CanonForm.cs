using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "canon_form")]
public sealed class CanonForm : MgrCard
{
    public CanonForm() : base(
        3,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CanonFormPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        Owner.Creature.GetPower<CanonFormPower>()?.QueueNewStack(
            Owner.PlayerCombatState!.TurnNumber);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
