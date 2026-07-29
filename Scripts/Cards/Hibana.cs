using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "hibana")]
public sealed class Hibana : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public Hibana() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
            return;

        var prompt = new LocString(
            "cards",
            "SLAY_THE_SPIRE2_MGR_MOD_CARD_NOTE_TRANSMUTATION_CHOOSE");
        var prefs = new CardSelectorPrefs(prompt, 1);
        CardModel? chosen = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).FirstOrDefault();
        if (chosen is null)
            return;

        NoteKind kind = CardNoteResolver.Resolve(chosen);
        await CardCmd.Exhaust(choiceContext, chosen);
        await ChannelNote(choiceContext, kind);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
