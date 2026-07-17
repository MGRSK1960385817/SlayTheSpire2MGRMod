using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "prolonged_note")]
public sealed class ProlongedNote : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public ProlongedNote() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MgrPerformanceSystem.AddPerformancesToQueuedCards(Owner, 1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
