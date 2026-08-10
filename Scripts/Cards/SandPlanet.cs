using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "sand_planet")]
public sealed class SandPlanet : MgrCard
{
    protected override MgrKeywordKind KeywordKinds => MgrKeywordKind.AttackNote;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 2m)
    ];

    public SandPlanet() : base(
        2,
        CardType.Power,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<SandPlanetPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        for (int index = 0; index < DynamicVars["Notes"].IntValue; index++)
            await ChannelNote(choiceContext, NoteKind.Attack);
    }

    protected override void OnUpgrade() =>
        DynamicVars["Notes"].UpgradeValueBy(2m);
}
