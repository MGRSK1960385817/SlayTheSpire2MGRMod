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

    private const string SceneRoot = $"{Entry.ResPath}/scenes/characters";
    private const string ImageRoot = $"{Entry.ResPath}/images/characters";
    private const string CharacterScenePath = $"{SceneRoot}/Mgr_character.tscn";

    public override Color NameColor => ThemeColor;
    public override Color EnergyLabelOutlineColor => new(0.32f, 0.08f, 0.02f);
    public override Color MapDrawingColor => ThemeColor;
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 66;
    public override int StartingGold => 99;

    public override CharacterAssetProfile AssetProfile => new(
        Scenes: new CharacterSceneAssetSet(
            VisualsPath: CharacterScenePath,
            EnergyCounterPath: $"{SceneRoot}/Mgr_energy_counter.tscn",
            MerchantAnimPath: $"{SceneRoot}/Mgr_merchant.tscn",
            RestSiteAnimPath: $"{SceneRoot}/Mgr_rest_site.tscn"),
        Ui: new CharacterUiAssetSet(
            IconTexturePath: $"{ImageRoot}/Mgr_character_icon.png",
            IconOutlineTexturePath: $"{ImageRoot}/Mgr_character_icon_outline.png",
            CharacterSelectBgPath: $"{SceneRoot}/Mgr_character_select_bg.tscn",
            CharacterSelectIconPath: $"{ImageRoot}/Mgr_character_select_icon.png",
            CharacterSelectLockedIconPath: $"{ImageRoot}/Mgr_character_select_locked.png",
            MapMarkerPath: $"{ImageRoot}/Mgr_map_marker.png"));

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
            "vfx/vfx_attack_slash",
            "vfx/vfx_flying_slash",
            "vfx/vfx_star_attack"
        ];
    }
}
