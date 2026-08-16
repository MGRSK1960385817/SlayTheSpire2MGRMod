using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "yaaaaaa")]
public sealed class Yaaaaaa : MgrCard
{
    public override int MaxUpgradeLevel => int.MaxValue;

    public override string Title
    {
        get
        {
            string title = TitleLocString.GetFormattedText();
            if (CurrentUpgradeLevel <= 0)
                return title;

            string suffix = new LocString(
                "cards",
                "MGR_MOD_CARD_YAAAAAA.upgradeTitleSuffix")
                .GetFormattedText();
            return title + string.Concat(
                Enumerable.Repeat(suffix, CurrentUpgradeLevel));
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new CardsVar(3),
        new IntVar("RequiredCost", 1m)
    ];

    public Yaaaaaa() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(VfxCmd.bluntPath, null, "blunt_attack.mp3")
            .Execute(choiceContext);

        IEnumerable<CardModel> drawn = await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);
        CardModel[] cardsToDiscard = drawn
            .Where(card =>
                card.Pile?.Type == PileType.Hand &&
                card.EnergyCost.GetResolved() != DynamicVars["RequiredCost"].IntValue)
            .ToArray();
        await MgrDiscardPresentation.DiscardWithPreview(
            choiceContext,
            cardsToDiscard);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
