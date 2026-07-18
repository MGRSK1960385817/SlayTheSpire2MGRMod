using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "starry_drift")]
public sealed class StarryDrift : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new IntVar("Discard", 3m)
    ];

    public override bool IsStarryCard => true;

    public StarryDrift() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
        int discardCount = Math.Min(DynamicVars["Discard"].IntValue, handCount);
        if (discardCount == 0)
            return;

        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_STARRY_DRIFT_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, discardCount, discardCount);
        CardModel[] selected = (await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).ToArray();

        foreach (CardModel card in selected)
            await CardCmd.Discard(choiceContext, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
