using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "crime_and_punishment")]
public sealed class CrimeAndPunishment : MgrCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<FortePower>()
    ];

    public CrimeAndPunishment() : base(
        1,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<CrimeAndPunishmentPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
