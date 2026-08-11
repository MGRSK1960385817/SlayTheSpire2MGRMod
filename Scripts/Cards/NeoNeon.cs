using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "neo_neon")]
public sealed class NeoNeon : MgrCard
{
    protected override bool TransformsCardsIntoNotes => true;

    protected override MgrKeywordKind KeywordKinds =>
        MgrKeywordKind.Forte;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3),
        new PowerVar<FortePower>(1m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public NeoNeon() : base(
        1,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        int transformCount = Math.Min(
            DynamicVars.Cards.IntValue,
            PileType.Hand.GetPile(Owner).Cards.Count);
        if (transformCount <= 0)
            return;

        CardModel[] selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(
                CardSelectorPrefs.TransformSelectionPrompt,
                transformCount),
            filter: null,
            source: this)).ToArray();

        NoteKind[] kinds = selected
            .Select(static card => CardNoteResolver.Resolve(card))
            .ToArray();
        foreach (CardModel card in selected)
            await CardCmd.Exhaust(choiceContext, card);
        foreach (NoteKind kind in kinds)
            await ChannelNote(choiceContext, kind);

        if (kinds.Length != DynamicVars.Cards.IntValue ||
            kinds.Distinct().Count() != kinds.Length)
            return;

        decimal amount = DynamicVars["FortePower"].BaseValue;
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
