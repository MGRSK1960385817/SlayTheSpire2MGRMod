using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "rapid_claw")]
public sealed class RapidClaw : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        new IntVar("DamageLoss", 1m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public RapidClaw() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool shouldCopy = IsPhraseStart || IsPhraseEnd;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
        await ChannelNote(choiceContext, NoteKind.Attack);

        decimal damageLoss = DynamicVars["DamageLoss"].BaseValue;
        if (!shouldCopy || DynamicVars.Damage.BaseValue < damageLoss)
            return;

        CardModel clone = CreateClone();
        clone.DynamicVars.Damage.BaseValue -= damageLoss;
        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            clone,
            PileType.Hand,
            Owner);
        CardCmd.PreviewCardPileAdd(result);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
