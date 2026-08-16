using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "delusion_sketch")]
public sealed class DelusionSketch : MgrCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public DelusionSketch() : base(
        1,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        IEnumerable<CardModel> pool = Owner.Character.CardPool.GetUnlockedCards(
            Owner.UnlockState,
            Owner.RunState.CardMultiplayerConstraint);
        IReadOnlyList<CardModel> options =
            MgrWeightedCardRandom.CreateDistinctForCombat(
                Owner,
                pool,
                count: 3,
                Owner.RunState.Rng.CombatCardGeneration,
                MgrCardWeightProfile.Uniform);

        if (IsUpgraded)
        {
            foreach (CardModel option in options)
                CardCmd.Upgrade(option, CardPreviewStyle.None);
        }

        CardModel? chosen = null;
        try
        {
            chosen = await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                options,
                Owner,
                canSkip: false);
            if (chosen is not null)
                await MgrPerformanceSystem.EnqueueGeneratedCard(Owner, chosen);
        }
        finally
        {
            // The selection screen has released the temporary views after the
            // awaited command. Keep only the chosen card, now owned by Play.
            await Task.Yield();
            foreach (CardModel option in options)
            {
                if (!ReferenceEquals(option, chosen) &&
                    option.CombatState is not null &&
                    option.Pile is null)
                {
                    option.RemoveFromState();
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
    }
}
