using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "neo_neon")]
public sealed class NeoNeon : MgrCard
{
    protected override MgrKeywordKind KeywordKinds =>
        MgrKeywordKind.PowerNote | MgrKeywordKind.Forte;

    protected override MgrGoldGlowCondition GoldGlowConditions =>
        MgrGoldGlowCondition.PhraseStart;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", 2m),
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
        bool phraseStartedEmpty = IsPhraseStart;
        for (int index = 0; index < DynamicVars["Notes"].IntValue; index++)
            await ChannelNote(choiceContext, NoteKind.Power);

        if (!phraseStartedEmpty)
            return;

        decimal amount = DynamicVars["FortePower"].BaseValue;
        await PowerCmd.Apply<FortePower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
        await PowerCmd.Apply<TemporaryFortePower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
