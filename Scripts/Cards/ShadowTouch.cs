using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "shadow_touch")]
public sealed class ShadowTouch : MgrCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Pale>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public ShadowTouch() : base(
        1,
        CardType.Skill,
        CardRarity.Common,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        Pale pale = combatState.CreateCard<Pale>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(
            pale,
            PileType.Draw,
            Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
