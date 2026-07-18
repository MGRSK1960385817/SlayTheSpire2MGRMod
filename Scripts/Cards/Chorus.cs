using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "chorus")]
public sealed class Chorus : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 5m),
        new IntVar("StartingBonus", 0m)
    ];

    public Chorus() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int notes = DynamicVars["Notes"].IntValue;
        if (IsPhraseStart)
            notes += DynamicVars["StartingBonus"].IntValue;

        for (int index = 0; index < notes; index++)
            await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(1m);
        DynamicVars["StartingBonus"].UpgradeValueBy(2m);
    }
}
