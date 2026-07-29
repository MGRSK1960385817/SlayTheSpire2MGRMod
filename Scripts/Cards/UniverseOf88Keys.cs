using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "universe_of_88_keys")]
public sealed class UniverseOf88Keys : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<UniverseOf88KeysPower>(8m)
    ];

    public UniverseOf88Keys() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<UniverseOf88KeysPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["UniverseOf88KeysPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["UniverseOf88KeysPower"].UpgradeValueBy(2m);
}
