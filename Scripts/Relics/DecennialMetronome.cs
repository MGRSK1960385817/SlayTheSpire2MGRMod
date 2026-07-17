using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "decennial_metronome")]
public sealed class DecennialMetronome : ModRelicTemplate
{
    private const int ChordInterval = 10;
    private int _chordsThisCombat;

    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool ShowCounter => true;
    public override int DisplayAmount => ChordInterval - _chordsThisCombat;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/DecennialMetronome.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/DecennialMetronome_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/DecennialMetronome.png");

    public override Task BeforeCombatStart()
    {
        _chordsThisCombat = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public bool TryDoubleCurrentChord()
    {
        _chordsThisCombat++;
        bool doubles = _chordsThisCombat >= ChordInterval;
        if (doubles)
        {
            _chordsThisCombat = 0;
            Flash();
        }

        InvokeDisplayAmountChanged();
        return doubles;
    }
}
