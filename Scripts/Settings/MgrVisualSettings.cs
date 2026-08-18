using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace MGRMod.Settings;

/// <summary>
/// Local, persistent visual-performance preferences for MGR. These settings
/// never enter run state or multiplayer state: every client controls only the
/// way MGR characters are rendered on that machine.
/// </summary>
public sealed class MgrVisualSettingsData
{
    public bool DisableCharacterAnimation { get; set; }
}

public static class MgrVisualSettings
{
    private const string DataKey = "visual_settings";

    private static readonly ModSettingsValueBinding<MgrVisualSettingsData, bool>
        DisableCharacterAnimationBinding = new(
            Entry.ModId,
            DataKey,
            SaveScope.Global,
            static settings => settings.DisableCharacterAnimation,
            static (settings, value) => settings.DisableCharacterAnimation = value);

    private static bool _registered;

    public static bool ShouldPlayCharacterAnimation =>
        !_registered || !DisableCharacterAnimationBinding.Read();

    public static bool ShouldLoadCharacterEffects =>
        !_registered || !DisableCharacterAnimationBinding.Read();

    public static void Register()
    {
        if (_registered)
            return;

        ModDataStore.For(Entry.ModId).Register<MgrVisualSettingsData>(
            key: DataKey,
            fileName: "mgr_visual_settings.json",
            scope: SaveScope.Global,
            defaultFactory: static () => new MgrVisualSettingsData(),
            autoCreateIfMissing: true);

        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .WithTitle(Text("MGR_MOD_SETTINGS_UI_VISUAL_PAGE_TITLE", "视觉性能"))
                .WithModDisplayName(Text("MGR_MOD_SETTINGS_UI_TELEMETRY_MOD_NAME", "MGR模组"))
                .WithVisibleOnHostSurfaces(ModSettingsHostSurface.All)
                .AddSection("visual_performance", section => section
                    .WithTitle(Text("MGR_MOD_SETTINGS_UI_VISUAL_SECTION_TITLE", "角色视觉"))
                    .AddToggle(
                        "disable_character_animation",
                        Text("MGR_MOD_SETTINGS_UI_DISABLE_CHARACTER_ANIMATION", "关闭人物动画"),
                        DisableCharacterAnimationBinding,
                        Text(
                            "MGR_MOD_SETTINGS_UI_DISABLE_CHARACTER_ANIMATION_DESCRIPTION",
                            "同时关闭逐帧人物动画及角色周围常驻特效；下次创建战斗人物时生效。"))));

        _registered = true;
    }

    private static ModSettingsText Text(string key, string fallback) =>
        ModSettingsText.LocString("settings_ui", key, fallback);
}
