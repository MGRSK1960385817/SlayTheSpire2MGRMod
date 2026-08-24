using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "metronome")]
public sealed class Metronome : ModRelicTemplate
{
    private const int ChordInterval = 7;
    private bool _isActivating;
    private int _chordsTriggered;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => IsActivating
        ? ChordInterval
        : ChordsTriggered;

    private bool IsActivating
    {
        get => _isActivating;
        set
        {
            AssertMutable();
            _isActivating = value;
            UpdateDisplay();
        }
    }

    [SavedProperty]
    public int ChordsTriggered
    {
        get => _chordsTriggered;
        private set
        {
            AssertMutable();
            _chordsTriggered = Math.Max(0, value) % ChordInterval;
            UpdateDisplay();
        }
    }

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/Metronome.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/Metronome_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/Metronome.png");

    public bool TryDoubleCurrentChord()
    {
        int nextCount = ChordsTriggered + 1;
        bool doubles = nextCount >= ChordInterval;
        ChordsTriggered = nextCount;
        if (doubles)
            TaskHelper.RunSafely(DoActivateVisuals());
        return doubles;
    }

    private void UpdateDisplay()
    {
        Status = !IsActivating && ChordsTriggered == ChordInterval - 1
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private async Task DoActivateVisuals()
    {
        IsActivating = true;
        Flash();
        await Cmd.Wait(MgrVisualTiming.ScaleBlockingVisualWait(Owner, 1f));
        IsActivating = false;
    }
}
