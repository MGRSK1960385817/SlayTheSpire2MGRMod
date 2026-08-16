using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "book_of_grudges")]
public sealed class BookOfGrudges : ModRelicTemplate
{
    private const int BaseAttackNotes = 2;
    private const int HpLostPerExtraNote = 5;
    private int _totalHpLost;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Notes", BaseAttackNotes)
    ];

    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool IsAllowed(IRunState runState) =>
        IsBeforeAct3TreasureChest(runState);
    public override bool ShowCounter => true;
    public override int DisplayAmount => CombatStartAttackNotes;

    [SavedProperty]
    public int TotalHpLost
    {
        get => _totalHpLost;
        set
        {
            AssertMutable();
            _totalHpLost = Math.Max(0, value);
            DynamicVars["Notes"].BaseValue = CombatStartAttackNotes;
            InvokeDisplayAmountChanged();
        }
    }

    public int CombatStartAttackNotes =>
        BaseAttackNotes + TotalHpLost / HpLostPerExtraNote;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/BookOfGrudges.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/BookOfGrudges_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/BookOfGrudges.png");

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Owner.Creature && delta < 0m)
            TotalHpLost += decimal.ToInt32(decimal.Negate(delta));

        return Task.CompletedTask;
    }
}
