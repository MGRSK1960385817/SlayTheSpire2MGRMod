using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "opening_tuning")]
public sealed class SetTheTone : MgrCard
{
    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseStart;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move)
    ];

    public SetTheTone() : base(
        1,
        CardType.Attack,
        CardRarity.Common,
        TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool isStarting = IsPhraseStart;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        if (isStarting)
        {
            CardModel[] hand = PileType.Hand.GetPile(Owner).Cards
                .Where(card => card.IsUpgradable)
                .ToArray();
            CardCmd.Upgrade(hand, CardPreviewStyle.HorizontalLayout);
            return;
        }

        CardModel[] eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card.IsUpgradable)
            .ToArray();
        if (eligible.Length == 0)
            return;

        CardModel? chosen = Owner.RunState.Rng.CombatCardSelection.NextItem(eligible);
        if (chosen is not null)
            CardCmd.Upgrade(chosen);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
