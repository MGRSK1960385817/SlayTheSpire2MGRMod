using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "folk_rhymes")]
public sealed class SatelliteGirl : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockPerChord", 1m)
    ];

    public SatelliteGirl() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<FolkRhymesPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["BlockPerChord"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade()
    {
        DynamicVars["BlockPerChord"].UpgradeValueBy(1m);
    }
}
