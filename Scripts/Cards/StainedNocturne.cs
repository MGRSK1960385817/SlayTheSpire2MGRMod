using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "stained_nocturne")]
public sealed class StainedNocturne : MgrCard
{
    public StainedNocturne() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StainedNocturnePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        await ChannelNote(choiceContext, NoteKind.Curse);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
