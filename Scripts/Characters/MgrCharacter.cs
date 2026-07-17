using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

namespace SlayTheSpire2MGRMod.Characters;

[RegisterCharacter]
public sealed class MgrCharacter : ModCharacterTemplate<MgrCardPool, MgrRelicPool, MgrPotionPool>
{
    public static readonly Color ThemeColor = new(1f, 0.43f, 0f);

    internal const string SceneRoot = $"{Entry.ResPath}/scenes/characters";
    internal const string ImageRoot = $"{Entry.ResPath}/images/characters";
    private const string CharacterScenePath = $"{SceneRoot}/Mgr_character.tscn";

    public override Color NameColor => ThemeColor;
    public override Color EnergyLabelOutlineColor => new(0.32f, 0.08f, 0.02f);
    public override Color MapDrawingColor => ThemeColor;
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 66;
    public override int StartingGold => 99;

    public override CharacterAssetProfile AssetProfile => MgrCharacterAssets.Profile;

    // Development fallback only. It prevents missing non-MGR assets from blocking the first load.
    public override string? PlaceholderCharacterId => "ironclad";
    public override bool RequiresEpochAndTimeline => false;
    public override float AttackAnimDelay => 0f;
    public override float CastAnimDelay => 0f;

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(CharacterScenePath);
    }

    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_slash"
        ];
    }
}
