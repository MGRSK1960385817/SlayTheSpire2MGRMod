using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "puppet_clown")]
public sealed class PuppetClown : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    public PuppetClown() : base(
        0,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override bool ShouldGlowGoldInternal =>
        base.ShouldGlowGoldInternal ||
        CombatState is not null && GetNearestCurseInDrawPile() is not null;

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay);

        // Replay repeats the numerical effect, but a physical card can exchange
        // its pile position only once during a play series.
        if (!cardPlay.IsFirstInSeries)
            return;

        CardPile drawPile = PileType.Draw.GetPile(Owner);
        CardModel? curse = GetNearestCurseInDrawPile();
        if (curse is null)
            return;

        int curseDrawIndex = drawPile.Cards.ToList().IndexOf(curse);

        // When the card itself is resolving from the Performance rack, exchange
        // the two real combat models in place. A Curse without printed
        // Performance enters with exactly one remaining trigger instead of
        // inheriting Puppet Clown's old counter.
        if (MgrPerformanceSystem.IsResolvingPerformance(this))
        {
            // Reserve the logical replacement before changing either physical
            // pile. If the scheduler rejects it, leave both cards untouched.
            if (!MgrPerformanceSystem.QueueResolvingCardReplacement(this, curse))
                return;

            MgrBlueCardVfx.SpawnPuppetClownSwap(Owner.Creature);
            await CardPileCmd.Add(
                curse,
                PileType.Play,
                skipVisuals: true);
            await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Bottom);
            if (Pile == drawPile)
                ReinsertAt(drawPile, this, curseDrawIndex);
            return;
        }

        // A normally played Performance Puppet Clown has not been registered in
        // the rack yet (that happens in AfterCardPlayed). Queue the Curse as the
        // resulting entry now, move Puppet Clown to the Curse's draw position,
        // and let the played-card callback present the replacement rack entry.
        if (MgrPerformanceSystem.IsPerformanceCard(this))
        {
            // The entry itself is created by AfterCardPlayed, so reserve its
            // replacement first and only then perform the physical exchange.
            if (!MgrPerformanceSystem.QueuePlayedCardReplacement(this, curse))
                return;

            MgrBlueCardVfx.SpawnPuppetClownSwap(Owner.Creature);
            await CardPileCmd.Add(
                curse,
                PileType.Play,
                skipVisuals: true);
            await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Bottom);
            if (Pile == drawPile)
                ReinsertAt(drawPile, this, curseDrawIndex);
            return;
        }

        // This is the same native pile-transfer path used by Regent's Make It
        // So, producing the clear card-to-hand flight instead of a draw action.
        // Do not restore the Curse to a hand index captured by
        // OnEnqueuePlayVfx: that callback runs only on the client initiating
        // the manual play, before PlayCardAction is synchronized. CardPileCmd
        // must be the sole
        // owner of the resulting hand order so every peer receives the same
        // deterministic model state.
        MgrBlueCardVfx.SpawnPuppetClownSwap(Owner.Creature);
        await CardPileCmd.Add(curse, PileType.Hand);
        CardPile handPile = PileType.Hand.GetPile(Owner);
        if (curse.Pile != handPile)
            return;

        // Move this played card into the exact draw-pile slot vacated by the
        // curse. The native Add call supplies the short pile-entry animation;
        // the silent reinsert only corrects its underlying draw order.
        await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Bottom);
        if (Pile == drawPile)
            ReinsertAt(drawPile, this, curseDrawIndex);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);

    private CardModel? GetNearestCurseInDrawPile() =>
        PileType.Draw.GetPile(Owner).Cards
            .FirstOrDefault(card => card.Type == CardType.Curse);

    private static void ReinsertAt(
        CardPile pile,
        CardModel card,
        int requestedIndex)
    {
        if (!pile.Cards.Contains(card))
            return;

        pile.RemoveInternal(card, silent: true);
        int index = Math.Clamp(requestedIndex, 0, pile.Cards.Count);
        pile.AddInternal(card, index, silent: true);
        pile.InvokeContentsChanged();
    }
}
