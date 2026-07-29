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
        new IntVar("Performance", 2m)
    ];

    public override int InitialPerformanceTurns =>
        DynamicVars["Performance"].IntValue;

    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseEnd;

    public LittleMiracles() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsAutoPlay && IsPhraseEnd)
            MgrPerformanceSystem.AddPendingEnqueueBonus(this, 1);

        if (IsPhraseStart)
        {
            for (int index = 0; index < DynamicVars.Cards.IntValue; index++)
                await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, Owner);
        }
    }

    protected override void OnUpgrade() =>
        DynamicVars["Performance"].UpgradeValueBy(1m);
}
