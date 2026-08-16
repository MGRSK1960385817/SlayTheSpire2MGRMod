using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "give_it_to_you")]
public sealed class GiveItToYou : MgrCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    protected override MgrKeywordKind KeywordKinds => MgrKeywordKind.Chord;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    public GiveItToYou() : base(
        0,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.AnyAlly)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var targetPlayer = cardPlay.Target.Player ?? throw new InvalidOperationException(
            "Give It to You requires a player target.");

        GiveItToYouPower? existing = Owner.Creature.Powers
            .OfType<GiveItToYouPower>()
            .FirstOrDefault(power => power.HasPlayerTarget(targetPlayer));
        if (existing is not null)
        {
            existing.Flash();
            return;
        }

        GiveItToYouPower? power = await PowerCmd.Apply<GiveItToYouPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        if (power is not null)
            power.PlayerTarget = targetPlayer;
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
