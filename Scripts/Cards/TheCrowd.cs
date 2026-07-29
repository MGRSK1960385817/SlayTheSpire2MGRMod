using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "the_crowd")]
public sealed class TheCrowd : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    public TheCrowd() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsUpgraded)
        {
            await ChannelRandomNotes(choiceContext, 2);
            return;
        }

        if (CombatState is not { } combatState)
            return;

        IReadOnlyList<CardModel> options =
        [
            combatState.CreateCard<TheCrowdChoice0>(Owner),
            combatState.CreateCard<TheCrowdChoice1>(Owner),
            combatState.CreateCard<TheCrowdChoice2>(Owner),
            combatState.CreateCard<TheCrowdChoice3>(Owner)
        ];
        CardModel? chosen = null;
        try
        {
            chosen = await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                options,
                Owner,
                canSkip: false);
            if (chosen is TheCrowdChoice choice)
                await ChannelRandomNotes(choiceContext, choice.NoteCount);
        }
        finally
        {
            await Task.Yield();
            foreach (CardModel option in options)
            {
                if (option.CombatState is not null && option.Pile is null)
                    option.RemoveFromState();
            }
        }
    }

    private async Task ChannelRandomNotes(PlayerChoiceContext choiceContext, int count)
    {
        for (int index = 0; index < count; index++)
            await MgrNoteSystem.ChannelRandomBasicNote(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
    }
}
