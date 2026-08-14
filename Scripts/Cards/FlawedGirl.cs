using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Cards.Choices;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "flawed_girl")]
public sealed class FlawedGirl : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<FlawedGirl0>(),
        HoverTipFactory.FromCard<FlawedGirl1>()
    ];

    public FlawedGirl() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        List<CardModel> options =
        [
            combatState.CreateCard<FlawedGirl0>(Owner),
            combatState.CreateCard<FlawedGirl1>(Owner)
        ];
        using IDisposable screenFilter =
            MgrSelectionScreenVfx.BeginGlitch(Owner);
        try
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                options,
                Owner,
                prefs)).FirstOrDefault();

            if (chosen is INoteSlotChoice choice)
                await choice.Apply(choiceContext, this);
        }
        finally
        {
            // The selection screen has released its card nodes after the awaited
            // command. Remove both temporary combat models on the following tick.
            await Task.Yield();
            foreach (CardModel option in options)
            {
                if (option.CombatState is not null)
                    option.RemoveFromState();
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
