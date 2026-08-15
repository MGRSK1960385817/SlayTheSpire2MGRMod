using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "samsara")]
public sealed class Samsara : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VigorPower>(1m)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Ethereal];

    protected override MgrKeywordKind KeywordKinds =>
        MgrKeywordKind.AttackNote | MgrKeywordKind.Chord;

    public Samsara() : base(
        1,
        CardType.Power,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<SamsaraPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Ethereal);
}
