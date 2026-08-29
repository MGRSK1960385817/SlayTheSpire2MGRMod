using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "byakkoya_girl")]
public sealed class ByakkoyaGirl : MgrCard
{
    protected override bool TransformsCardsIntoNotes => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 2m),
        new CardsVar(1)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public ByakkoyaGirl() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IEnumerable<CardModel> drawnCards = await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);

        // Let the native draw presentation settle before selecting and
        // exhausting the lowest-cost hand card. The hand snapshot deliberately
        // remains after this visual-only beat, so the chosen candidate still
        // comes from the latest authoritative pile state.
        if (drawnCards.Any())
        {
            await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
                this,
                MgrVisualTuning.ByakkoyaGirl.DrawToExhaustPauseSeconds));
        }

        List<CardModel> hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        int lowestCost = hand.Min(GetCurrentEnergyCost);
        List<CardModel> candidates = hand
            .Where(card => GetCurrentEnergyCost(card) == lowestCost)
            .ToList();

        // Unplayable Curses and Statuses both resolve to a practical cost of 0.
        // When both categories tie, remove Status candidates so Curse cards get
        // the requested priority. Other genuinely 0-cost cards remain eligible.
        if (candidates.Any(card => card.Type == CardType.Curse) &&
            candidates.Any(card => card.Type == CardType.Status))
        {
            candidates.RemoveAll(card => card.Type == CardType.Status);
        }

        CardModel chosen = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates)
            ?? candidates[0];
        NoteKind kind = CardNoteResolver.Resolve(chosen);
        await CardCmd.Exhaust(choiceContext, chosen);
        await ChannelNote(choiceContext, kind);
    }

    private static int GetCurrentEnergyCost(CardModel card) =>
        card.EnergyCost.GetAmountToSpend();

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
