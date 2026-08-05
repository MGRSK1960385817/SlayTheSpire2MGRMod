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
        new CardsVar(2)
    ];

    public override bool IsStarryCard => true;

    public Futariboshi() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        int discardCount = Math.Min(DynamicVars.Cards.IntValue, hand.Count);

        if (discardCount > 0)
        {
            CardModel[] discarded;
            if (IsUpgraded)
            {
                var prompt = new LocString(
                    "cards",
                    "SLAY_THE_SPIRE2_MGR_MOD_CARD_FUTARIBOSHI_CHOOSE");
                var prefs = new CardSelectorPrefs(prompt, discardCount);
                discarded = (await CardSelectCmd.FromHand(
                    choiceContext,
                    Owner,
                    prefs,
                    null,
                    this)).ToArray();
            }
            else
            {
                var selected = new List<CardModel>(discardCount);
                for (int index = 0; index < discardCount; index++)
                {
                    CardModel chosen =
                        Owner.RunState.Rng.CombatCardSelection.NextItem(hand) ??
                        throw new InvalidOperationException(
                            "A non-empty hand candidate list produced no random card.");
                    selected.Add(chosen);
                    hand.Remove(chosen);
                }

                discarded = selected.ToArray();
            }

            await CardCmd.Discard(choiceContext, discarded);
        }

        await ChannelNote(choiceContext, NoteKind.Starry);
    }

    protected override void OnUpgrade()
    {
    }
}
