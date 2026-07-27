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

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "dizzy_and_giddy")]
public sealed class DizzyAndGiddy : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public DizzyAndGiddy() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
        if (handCount == 0)
            return;

        int maxSelect = Math.Min(DynamicVars.Cards.IntValue, handCount);
        const int minSelect = 0;
        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_DIZZY_AND_GIDDY_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, minSelect, maxSelect);
        CardModel[] selected = (await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).ToArray();

        foreach (CardModel card in selected)
        {
            NoteKind kind = CardNoteResolver.Resolve(card);
            await CardCmd.Discard(choiceContext, card);
            await ChannelNote(choiceContext, kind);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
