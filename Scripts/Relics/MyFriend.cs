using MegaCrit.Sts2.Core.Entities.Relics;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Relics;

[RegisterRelic(typeof(MgrRelicPool), StableEntryStem = "my_friend")]
[RegisterCharacterStarterRelic(typeof(MgrCharacter), Order = 0)]
[RegisterTouchOfOrobasRefinement(typeof(JourneyWithMe))]
public sealed class MyFriend : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<string> RegisteredKeywordIds =>
    [
        MgrKeywords.BasicNotes,
        MgrKeywords.AttackNote,
        MgrKeywords.SkillNote,
        MgrKeywords.PowerNote,
        MgrKeywords.StatusNote,
        MgrKeywords.CurseNote
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/MyFriend.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/MyFriend_outline.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/MyFriend.png");

    // MgrNoteSystem seeds Attack, Skill and Power notes at combat start so reset and
    // chord resolution happen in one deterministic, multiplayer-aware hook.
}
