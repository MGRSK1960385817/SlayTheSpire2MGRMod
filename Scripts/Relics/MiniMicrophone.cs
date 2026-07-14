using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "mini_microphone")]
[RegisterCharacterStarterRelic(typeof(MgrCharacter), Order = 0)]
public sealed class MiniMicrophone : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/MiniMicrophone.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/MiniMicrophone_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/MiniMicrophone.png");

    // MgrNoteSystem seeds Attack, Skill and Power notes at combat start so reset and
    // chord resolution happen in one deterministic, multiplayer-aware hook.
}
