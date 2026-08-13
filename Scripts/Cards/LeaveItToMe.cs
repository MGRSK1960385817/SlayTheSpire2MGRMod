using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "leave_it_to_me")]
public sealed class LeaveItToMe : MgrCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Concat([EnergyHoverTip]);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(2)
    ];

    public LeaveItToMe() : base(
        1,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.AllAllies)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        Player[] livingPlayers = CombatState.Players
            .Where(player => player.Creature.IsAlive)
            .ToArray();

        foreach (Player player in livingPlayers)
        {
            await CardPileCmd.DrawWithoutBlockingOnOtherPlayers(
                choiceContext,
                DynamicVars.Cards.IntValue,
                player,
                this);
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, player);
        }

        // The drawback belongs to this card's owner. The number of Curses is
        // based on every currently living player, including the owner.
        foreach (Player _ in livingPlayers)
            await MgrCurseUtils.AddRandomCurseToCombat(Owner, PileType.Discard);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
