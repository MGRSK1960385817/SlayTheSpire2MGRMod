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

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "futariboshi")]
public sealed class Futariboshi : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    public override bool IsStarryCard => true;

    public Futariboshi() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
        int maxSelect = Math.Min(DynamicVars.Cards.IntValue, handCount);
        if (maxSelect == 0)
            return;

        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_FUTARIBOSHI_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, 0, maxSelect);
        CardModel[] selected = (await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).ToArray();

        foreach (CardModel card in selected)
        {
            await CardCmd.Discard(choiceContext, card);
            await ChannelNote(choiceContext, NoteKind.Starry);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
