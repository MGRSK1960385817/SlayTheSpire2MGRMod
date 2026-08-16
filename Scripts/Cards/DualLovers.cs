using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "dual_lovers")]
public sealed class DualLovers : MgrCard
{
    public DualLovers() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (CardModel card in PileType.Draw.GetPile(Owner).Cards.ToArray())
        {
            if (card.Type == CardType.Attack)
                MgrPerformanceSystem.GrantAdditionalPerformances(card, 1);
        }

        await PowerCmd.Apply<AttackNoteSilencePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
