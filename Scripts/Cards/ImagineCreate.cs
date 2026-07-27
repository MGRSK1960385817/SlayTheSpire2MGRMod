using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

// Keep the published entry stem so existing saves and Dusty Tome registration
// continue to identify this card after its development code name changes.
[RegisterCard(typeof(MgrCardPool), StableEntryStem = "unnamed_ancient")]
[RegisterDustyTomeCard(typeof(MgrCharacter))]
public sealed class ImagineCreate : MgrCard
{
    public ImagineCreate() : base(
        1,
        CardType.Power,
        CardRarity.Ancient,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<ATaleOfMinePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
