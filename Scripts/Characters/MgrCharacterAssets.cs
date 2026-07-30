using STS2RitsuLib.Scaffolding.Characters;

namespace SlayTheSpire2MGRMod.Characters;

/// <summary>
/// Complete character asset profile. Each STS2 UI role receives an intentional asset
/// instead of reusing the STS1 character-button bow for every surface.
/// </summary>
internal static class MgrCharacterAssets
{
    private const string TransitionMaterial = "res://materials/transitions/silent_transition_mat.tres";
    private const string SilentTransitionSfx = "event:/sfx/ui/wipe_silent";
    private const string SilentAttackSfx = "event:/sfx/characters/silent/silent_attack";
    private const string SilentCastSfx = "event:/sfx/characters/silent/silent_cast";
    private const string SilentDeathSfx = "event:/sfx/characters/silent/silent_die";

    private static readonly CharacterAssetProfile BaseProfile = CharacterAssetProfiles.Ironclad();

    internal static CharacterAssetProfile Profile { get; } = BaseProfile
        .WithScenes(new CharacterSceneAssetSet(
            VisualsPath: $"{MgrCharacter.SceneRoot}/Mgr_character.tscn",
            EnergyCounterPath: $"{MgrCharacter.SceneRoot}/Mgr_energy_counter.tscn",
            MerchantAnimPath: $"{MgrCharacter.SceneRoot}/Mgr_merchant.tscn",
            RestSiteAnimPath: $"{MgrCharacter.SceneRoot}/Mgr_rest_site.tscn"))
        .WithUi(new CharacterUiAssetSet(
            IconTexturePath: $"{MgrCharacter.ImageRoot}/Mgr_character_icon.png",
            IconOutlineTexturePath: $"{MgrCharacter.ImageRoot}/Mgr_character_icon_outline.png",
            IconPath: $"{MgrCharacter.SceneRoot}/Mgr_character_icon.tscn",
            CharacterSelectBgPath: $"{MgrCharacter.SceneRoot}/Mgr_character_select_bg.tscn",
            CharacterSelectIconPath: $"{MgrCharacter.ImageRoot}/Mgr_character_select.png",
            CharacterSelectLockedIconPath: $"{MgrCharacter.ImageRoot}/Mgr_character_select_locked.png",
            CharacterSelectTransitionPath: TransitionMaterial,
            MapMarkerPath: $"{MgrCharacter.ImageRoot}/Mgr_map_marker.png"))
        .WithAudio(new CharacterAudioAssetSet(
            CharacterSelectSfx: MgrAudio.CharacterSelectSfx,
            CharacterTransitionSfx: SilentTransitionSfx,
            AttackSfx: SilentAttackSfx,
            CastSfx: SilentCastSfx,
            DeathSfx: SilentDeathSfx))
        .WithVisualCues(MgrCharacterAnimation.CombatCues);
}
