using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "final_shot")]
public sealed class FinalShot : MgrCard
{
    internal const string RepeatSelectionKey = "MGR_MOD_CARD_FINAL_SHOT.selectionScreenPrompt";
    internal const string TitleKey = "MGR_MOD_CARD_FINAL_SHOT.title";
    private const int RequiredAttacks = 2;

    protected override bool TransformsCardsIntoNotes => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && HasEnoughOtherAttacks;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(11m, ValueProp.Move)
    ];

    public FinalShot() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        ArgumentNullException.ThrowIfNull(target);
        if (!CanHit(target))
            return;

        bool converted = HasEnoughOtherAttacks && await TryConvertAttacks(choiceContext);
        if (!CanHit(target))
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitCount(converted ? 2 : 1)
            .WithHitFx(converted ? "vfx/vfx_attack_blunt" : "vfx/vfx_attack_slash",
                null, converted ? "blunt_attack.mp3" : null)
            .Execute(choiceContext);
    }

    private async Task<bool> TryConvertAttacks(PlayerChoiceContext choiceContext)
    {
        var prompt = LocString.GetIfExists("cards", RepeatSelectionKey) ??
            new LocString("cards", TitleKey);
        var prefs = new CardSelectorPrefs(prompt, 0, RequiredAttacks)
        {
            RequireManualConfirmation = true,
            Cancelable = true
        };
        CardModel[] selected = (await CardSelectCmd.FromHand(
            choiceContext, Owner, prefs, IsOtherAttack, this)).ToArray();
        if (selected.Length != RequiredAttacks)
            return false;

        foreach (CardModel card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        foreach (CardModel card in selected)
            await MgrNoteSystem.ResolveCardTransformation(choiceContext, Owner, NoteKind.Attack);

        return true;
    }

    private bool HasEnoughOtherAttacks =>
        PileType.Hand.GetPile(Owner).Cards.Count(IsOtherAttack) >= RequiredAttacks;

    private bool IsOtherAttack(CardModel card) => card != this && card.Type == CardType.Attack;

    private bool CanHit(Creature target) =>
        !MgrNoteSystem.ShouldStopNoteSequence(Owner) &&
        CombatState?.HittableEnemies.Contains(target) == true;

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}
