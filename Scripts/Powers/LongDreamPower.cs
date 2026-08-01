using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

/// <summary>
/// Restores every creature at the end of the caster's current side turn. Tower
/// 2's TemporaryStrengthPower restores at each affected creature's own side end,
/// which would incorrectly leave enemies weakened during their next turn.
/// </summary>
[RegisterPower]
public sealed class LongDreamPower : ModPowerTemplate
{
    private readonly Dictionary<Creature, decimal> _losses = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/LongDreamPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/LongDreamPower.png");

    public void RecordLoss(IEnumerable<Creature> creatures, decimal amount)
    {
        foreach (Creature creature in creatures)
        {
            _losses[creature] = _losses.TryGetValue(creature, out decimal current)
                ? current + amount
                : amount;
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
            return;

        Flash();
        foreach ((Creature creature, decimal amount) in _losses.ToArray())
        {
            if (!creature.IsAlive)
                continue;

            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                creature,
                amount,
                Owner,
                cardSource: null);
        }

        _losses.Clear();
        await PowerCmd.Remove(this);
    }
}
