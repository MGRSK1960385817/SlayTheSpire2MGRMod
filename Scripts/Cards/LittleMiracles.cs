using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "little_miracles")]
public sealed class LittleMiracles : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3),
        new IntVar("Performance", 3m),
        new IntVar("TailPerformanceBonus", 1m)
    ];

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseStart |
        MgrGoldGlowCondition.PhraseEnd;

    public LittleMiracles() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsAutoPlay && IsPhraseEnd)
        {
            MgrPerformanceSystem.AddPendingEnqueueBonus(
                this,
                DynamicVars["TailPerformanceBonus"].IntValue);
        }

        if (IsPhraseStart)
        {
            for (int index = 0; index < DynamicVars.Cards.IntValue; index++)
                await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TailPerformanceBonus"].UpgradeValueBy(1m);
    }
}
