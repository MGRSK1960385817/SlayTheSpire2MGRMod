using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "procrastination")]
public sealed class Procrastination : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 3m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public Procrastination() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    public override async Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context)
    {
        // PotionFactory returns a canonical database model. As with the
        // original random-potion procurement flow, the player must receive a
        // mutable combat instance rather than the shared canonical template.
        PotionModel potion = PotionFactory.CreateRandomPotionInCombat(
                context.Player,
                context.Player.RunState.Rng.CombatPotionGeneration,
                [])
            .ToMutable();
        await PotionCmd.TryToProcure(potion, context.Player);
    }

    protected override void OnUpgrade() =>
        DynamicVars["Performance"].UpgradeValueBy(-1m);
}
