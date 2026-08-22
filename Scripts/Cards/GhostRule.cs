using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "ghost_rule")]
public sealed class GhostRule : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public GhostRule() : base(
        0,
        CardType.Skill,
        CardRarity.Common,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CardModel[] selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(
                new LocString(
                    "cards",
                    "MGR_MOD_CARD_GHOST_RULE.selectionScreenPrompt"),
                0,
                DynamicVars.Cards.IntValue)
            {
                Cancelable = true,
                RequireManualConfirmation = true
            })).ToArray();

        if (selected.Length > 0)
        {
            await CardPileCmd.Add(
                selected,
                PileType.Draw,
                CardPilePosition.Top);
        }

        await MgrCurseUtils.AddRandomCurseToCombat(Owner, PileType.Discard);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
