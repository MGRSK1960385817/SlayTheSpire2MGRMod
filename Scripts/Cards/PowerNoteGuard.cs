using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "power_note_guard")]
public sealed class PowerNoteGuard : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("BlockPerNote", 3m)
    ];

    public PowerNoteGuard() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<PowerNoteBlockPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["BlockPerNote"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
