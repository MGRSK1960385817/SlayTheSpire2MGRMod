using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "book_of_grudges")]
public sealed class BookOfGrudges : ModRelicTemplate
{
    private const int DamagePerExtraNote = 5;
    private int _totalDamageTaken;

    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool ShowCounter => true;
    public override int DisplayAmount => TotalDamageTaken;

    [SavedProperty]
    public int TotalDamageTaken
    {
        get => _totalDamageTaken;
        set
        {
            AssertMutable();
            _totalDamageTaken = Math.Max(0, value);
            InvokeDisplayAmountChanged();
        }
    }

    public int CombatStartAttackNotes => 1 + TotalDamageTaken / DamagePerExtraNote;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/BookOfGrudges.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/BookOfGrudges_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/BookOfGrudges.png");

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner.Creature && result.UnblockedDamage > 0)
            TotalDamageTaken += result.UnblockedDamage;

        return Task.CompletedTask;
    }
}
