using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class UniverseOf88KeysPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/UniverseOf88KeysPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/UniverseOf88KeysPower.png");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.CombatState is not { } combatState)
            return;

        int chordTriggers = Owner.Player is { } player &&
            MgrCombatStateStore.TryGet(player, out MgrCombatState state)
                ? state.ChordTriggersThisTurn
                : 0;
        decimal damage = Math.Max(0m, Amount - 2m * chordTriggers);
        if (damage <= 0m)
            return;

        Flash();
        foreach (Creature enemy in combatState.HittableEnemies.ToArray())
        {
            await CreatureCmd.Damage(
                choiceContext,
                enemy,
                damage,
                ValueProp.Unpowered,
                Owner,
                cardSource: null,
                cardPlay: null);
        }
    }
}
