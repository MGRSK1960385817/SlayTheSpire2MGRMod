using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SlayTheSpire2MGRMod.Characters;

namespace SlayTheSpire2MGRMod.Cards;

public sealed class Romp : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public Romp() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        List<CardModel> generated = [];
        for (int index = 0; index < DynamicVars.Cards.IntValue; index++)
        {
            Confused confused = combatState.CreateCard<Confused>(Owner);
            if (IsUpgraded)
                CardCmd.Upgrade(confused, CardPreviewStyle.None);
            generated.Add(confused);
        }

        IReadOnlyList<CardPileAddResult> results = await CardPileCmd.AddGeneratedCardsToCombat(
            generated,
            PileType.Hand,
            Owner);
        CardCmd.PreviewCardPileAdd(results);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
