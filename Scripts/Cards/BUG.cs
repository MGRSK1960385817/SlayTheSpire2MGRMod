using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Cards.Choices;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "flexible_range")]
public sealed class BUG : MgrCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<BUG0>(),
        HoverTipFactory.FromCard<BUG1>()
    ];

    public BUG() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        List<CardModel> options =
        [
            combatState.CreateCard<BUG0>(Owner),
            combatState.CreateCard<BUG1>(Owner)
        ];

        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_FLEXIBLE_RANGE_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, 1);
        CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            options,
            Owner,
            prefs)).FirstOrDefault();

        if (chosen is INoteSlotChoice choice)
            await choice.Apply(choiceContext, this);

        // The selection screen has released its card nodes after the awaited
        // command. Remove both temporary combat models on the following tick.
        await Task.Yield();
        foreach (CardModel option in options)
        {
            if (option.CombatState is not null)
                option.RemoveFromState();
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
