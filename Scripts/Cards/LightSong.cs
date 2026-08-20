using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "light_song")]
public sealed class LightSong : MgrCard
{
    private int _performanceX;

    protected override bool HasEnergyCostX => true;

    public override int InitialPerformanceTurns =>
        checked(_performanceX + (IsUpgraded ? 1 : 0));

    internal override int GetPerformanceTurnsForResultRouting(ResourceInfo resources) =>
        checked(
            Math.Max(_performanceX, resources.EnergySpent) +
            (IsUpgraded ? 1 : 0) +
            MgrPerformanceModifierState.GetAdditionalPerformances(this));

    public LightSong() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsAutoPlay)
            _performanceX = ResolveEnergyXValue();

        using IDisposable lightSongVfx = MgrLightSongVfx.Begin(Owner);
        await MoveFromHandToDraw(choiceContext);
        await MoveFromDrawToDiscard(choiceContext);
        await MoveFromDiscardToHand(choiceContext);
    }

    private async Task MoveFromHandToDraw(PlayerChoiceContext choiceContext)
    {
        if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
            return;

        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            CreateSelectionPrefs("LIGHT_SONG_CHOOSE_HAND"),
            null,
            this)).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Add(selected, PileType.Draw);
    }

    private async Task MoveFromDrawToDiscard(PlayerChoiceContext choiceContext)
    {
        if (PileType.Draw.GetPile(Owner).Cards.Count == 0)
            return;

        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Draw.GetPile(Owner),
            Owner,
            CreateSelectionPrefs("LIGHT_SONG_CHOOSE_DRAW"))).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Add(selected, PileType.Discard);
    }

    private async Task MoveFromDiscardToHand(PlayerChoiceContext choiceContext)
    {
        if (PileType.Discard.GetPile(Owner).Cards.Count == 0)
            return;

        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            CreateSelectionPrefs("LIGHT_SONG_CHOOSE_DISCARD"))).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Add(selected, PileType.Hand);
    }

    private static CardSelectorPrefs CreateSelectionPrefs(string key) =>
        new(new LocString("cards", $"MGR_MOD_CARD_{key}"), 1);

    public override Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context)
    {
        _performanceX = 0;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
    }
}
