using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Powers;

[RegisterPower]
public sealed class FrenzyPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/FrenzyPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/FrenzyPower.png");

    public override bool TryModifyKeywordsInCombat(
        CardModel card,
        ISet<CardKeyword> keywords)
    {
        return card.Owner == Owner.Player &&
            card.Type is CardType.Status or CardType.Curse &&
            keywords.Remove(CardKeyword.Unplayable);
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;
        if (card.Owner != Owner.Player ||
            card.Type is not (CardType.Status or CardType.Curse) ||
            !card.GetKeywordsWithSources(KeywordSources.Local)
                .Contains(CardKeyword.Unplayable))
        {
            return;
        }

        Flash();
        MgrAbilityVfx.PlayOfferingBlood(Owner);
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            2m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner,
            cardSource: card,
            cardPlay: cardPlay);
    }
}
