using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "performance_imprinting")]
public sealed class Resonate : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public Resonate() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
            return;

        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_PERFORMANCE_IMPRINTING_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, 1);
        CardModel? chosen = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).FirstOrDefault();

        if (chosen is null)
            return;

        MgrPerformanceSystem.GrantAdditionalPerformances(chosen, 1);
        CardCmd.Preview(chosen);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
