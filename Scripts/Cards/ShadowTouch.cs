using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

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
        new BlockVar(4m, ValueProp.Move),
        new CardsVar(1)
    ];

    public override bool GainsBlock => true;

    public ShadowTouch() : base(
        0,
        CardType.Skill,
        CardRarity.Common,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        Pale pale = combatState.CreateCard<Pale>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(
            pale,
            PileType.Draw,
            Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}
