using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "lonely_universe")]
public sealed class LonelyUniverse : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 3m)
    ];

    public override bool IsStarryCard => true;

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public LonelyUniverse() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsFirstInSeries &&
            !MgrPerformanceSystem.IsResolvingPerformance(this))
        {
            MgrBlueCardVfx.SpawnLonelyUniverse(Owner.Creature);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
