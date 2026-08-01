using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "byakkoya_girl")]
public sealed class ByakkoyaGirl : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new IntVar("Performance", 2m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public ByakkoyaGirl() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
            return;

        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_BYAKKOYA_GIRL_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, 1);
        CardModel? chosen = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).FirstOrDefault();
        if (chosen is null)
            return;

        NoteKind kind = CardNoteResolver.Resolve(chosen);
        await CardCmd.Exhaust(choiceContext, chosen);
        await ChannelNote(choiceContext, kind);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(1m);
    }
}
