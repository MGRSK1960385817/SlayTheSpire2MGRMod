using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

/// <summary>
/// Temporary Dusty Tome Ancient card. Replace its name and effect after the
/// second Ancient design is finalized; its stable registration key can remain.
/// </summary>
[RegisterCard(typeof(MgrCardPool), StableEntryStem = "unnamed_ancient")]
[RegisterDustyTomeCard(typeof(MgrCharacter))]
public sealed class UnnamedAncient : MgrCard
{
    public UnnamedAncient() : base(1, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade()
    {
    }
}
