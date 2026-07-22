using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "shatter")]
public sealed class OtomeDissection : MgrCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
        new IntVar("Threshold", 5m),
        new PowerVar<FortePower>(1m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public OtomeDissection() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> cardsToExhaust = PileType.Hand.GetPile(Owner).Cards.ToList();
        foreach (CardModel card in cardsToExhaust)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            NoteKind kind = CardNoteResolver.Resolve(card);
            await CardCmd.Exhaust(choiceContext, card);
            await MgrNoteSystem.ChannelNote(choiceContext, Owner, kind);
        }

        if (cardsToExhaust.Count >= DynamicVars["Threshold"].IntValue)
        {
            await PowerCmd.Apply<FortePower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["FortePower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}
