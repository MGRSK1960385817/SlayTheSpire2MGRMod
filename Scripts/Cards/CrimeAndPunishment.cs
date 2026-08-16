using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MGRMod.Characters;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "crime_and_punishment")]
public sealed class CrimeAndPunishment : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("HpLoss", 4m)
    ];

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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int previousStacks = Owner.Creature
            .GetPower<CrimeAndPunishmentPower>()?.Amount ?? 0;
        CrimeAndPunishmentPower? power = await PowerCmd.Apply<CrimeAndPunishmentPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (power is null)
            return;

        int stacksAdded = Math.Max(0, power.Amount - previousStacks);
        power.DynamicVars["HpLoss"].BaseValue +=
            DynamicVars["HpLoss"].BaseValue * stacksAdded;
    }

    protected override void OnUpgrade() =>
        DynamicVars["HpLoss"].UpgradeValueBy(-1m);
}
