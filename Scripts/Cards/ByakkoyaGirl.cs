using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Compatibility;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "byakkoya_girl")]
public sealed class ByakkoyaGirl : MgrCard
{
    private const string DamagePerHitKey = "DamagePerHit";

    protected override bool TransformsCardsIntoNotes => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar(DamagePerHitKey, 4m),
        new CardsVar(1),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("DamageTriggers").WithMultiplier(
            static (card, _) => GetPreviewHitCount(card)),
        new IntVar("Performance", 0m) // Used by the previous descriptions pending translation.
    ];

    public ByakkoyaGirl() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (PileType.Hand.GetPile(Owner).Cards.Any(card => IsConversionTarget(this, card)))
        {
            var prompt = LocString.GetIfExists("cards", "MGR_MOD_CARD_BYAKKOYA_GIRL.selectionScreenPrompt")
                ?? CardSelectorPrefs.TransformSelectionPrompt;
            CardModel? selected = (await CardSelectCmd.FromHand(
                choiceContext, Owner, new CardSelectorPrefs(prompt, DynamicVars.Cards.IntValue),
                filter: card => IsConversionTarget(this, card), source: this)).FirstOrDefault();
            if (selected is not null)
            {
                NoteKind kind = CardNoteResolver.Resolve(selected);
                await CardCmd.Exhaust(choiceContext, selected);
                await MgrNoteSystem.ResolveCardTransformation(choiceContext, Owner, kind);
            }
        }

        if (Owner.Creature.CombatState is not { } combatState)
            return;

        int hitCount = MgrNoteSystem.GetCardTransformationCount(Owner);
        decimal damage = DynamicVars[DamagePerHitKey].BaseValue;
        for (int hit = 0; hit < hitCount; hit++)
        {
            if (MgrNoteSystem.ShouldStopNoteSequence(Owner))
                break;

            var target = Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
            if (target is null)
                break;

            MgrSignatureVfx.SpawnByakkoyaHitFireflies(target, (int)damage);
            await MgrCrossVersionApi.Damage(
                choiceContext, target, damage, ValueProp.Unpowered, Owner.Creature,
                cardSource: null, cardPlay: null);
        }
    }

    private static decimal GetPreviewHitCount(CardModel source)
    {
        if (source.CombatState is null)
            return 0m;

        var player = source.Owner;
        bool canTransform = PileType.Hand.GetPile(player).Cards.Any(card =>
            IsConversionTarget(source, card) &&
            MgrNoteSystem.CanChannelNoteKind(player, CardNoteResolver.Resolve(card)));

        return MgrNoteSystem.GetCardTransformationCount(player) + (canTransform ? 1 : 0);
    }

    private static bool IsConversionTarget(CardModel source, CardModel card) =>
        card != source && card.Type != CardType.None;

    protected override void OnUpgrade() => DynamicVars[DamagePerHitKey].UpgradeValueBy(2m);
}
