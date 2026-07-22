using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "clear_throat")]
public sealed class HayakuNaru : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Performance", 2m)
    ];

    public override int InitialPerformanceTurns => DynamicVars["Performance"].IntValue;

    public HayakuNaru() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ClearThroatPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    public override async Task OnPerformanceFinished(
        PlayerChoiceContext choiceContext,
        PerformanceCompletionContext context)
    {
        if (context.Player.Creature.CombatState is not { } combatState)
            return;

        HayakuNaru copy = combatState.CreateCard<HayakuNaru>(context.Player);
        if (IsUpgraded)
            CardCmd.Upgrade(copy, CardPreviewStyle.None);
        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            copy,
            PileType.Discard,
            context.Player);
        CardCmd.PreviewCardPileAdd(result);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Performance"].UpgradeValueBy(1m);
    }
}
