using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class UniverseOf88KeysPower : ModPowerTemplate
{
    private const int DamageLostPerChord = 2;

    private sealed class CurrentDamageVar : DynamicVar
    {
        public CurrentDamageVar() : base("CurrentDamage", 0m)
        {
        }

        private int Value => _owner is UniverseOf88KeysPower power
            ? power.DisplayAmount
            : 0;

        protected override decimal GetBaseValueForIConvertible() => Value;

        public override string ToString() => Value.ToString();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CurrentDamageVar()
    ];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Amount remains the per-turn maximum so stacking another copy of the
    // Power still increases that maximum normally. Only the displayed amount
    // is reduced by Chords and it automatically returns to Amount when the
    // turn counter is reset.
    public override int DisplayAmount
    {
        get
        {
            if (!IsMutable || Owner.Player is not { } player ||
                !MgrCombatStateStore.TryGet(player, out MgrCombatState state))
            {
                return Math.Max(0, Amount);
            }

            return Math.Max(
                0,
                Amount - DamageLostPerChord * state.ChordTriggersThisTurn);
        }
    }

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

        decimal damage = DisplayAmount;
        if (damage <= 0m)
            return;

        Flash();
        Creature[] enemies = combatState.HittableEnemies.ToArray();
        int noteCount = (int)Math.Ceiling(damage / 2m);
        await MgrAbilityVfx.PlayUniverseOf88Keys(enemies, noteCount);
        foreach (Creature enemy in enemies)
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

    /// <summary>
    /// The current damage is derived from MGR's per-turn Chord counter. That
    /// counter lives outside PowerModel, so explicitly invalidate the native
    /// amount label whenever it changes or is reset.
    /// </summary>
    public void NotifyChordCounterChanged() => InvokeDisplayAmountChanged();
}
