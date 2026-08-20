using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Heat Abnormal's conditional heat-haze prelude. It samples and refracts the
/// battlefield below GlobalUi overlays, leaving played cards and popups crisp.
/// </summary>
public static class MgrHeatAbnormalVfx
{
    public static async Task Play(CardModel sourceCard, decimal damage)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(sourceCard.Owner) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        float intensity = MgrAttackVfx.ScaleByDamage(
            damage,
            MgrVisualTuning.HeatAbnormalVfx.ReferenceDamage,
            MgrVisualTuning.HeatAbnormalVfx.MinimumIntensity,
            MgrVisualTuning.HeatAbnormalVfx.IntensityGrowthPerDoubling,
            MgrVisualTuning.HeatAbnormalVfx.MaximumIntensity);
        var underlay = new Control
        {
            Name = "MgrHeatAbnormalUnderlay",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        underlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        underlay.AddChild(new BackBufferCopy
        {
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        });
        underlay.AddChild(new MgrHeatAbnormalWaveVisual(intensity));
        globalUi.AddChildSafely(underlay);
        globalUi.MoveChild(
            underlay,
            Math.Max(0, globalUi.Overlays.GetIndex()));

        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.HeatAbnormalVfx.ImpactBeatSeconds));
    }
}

internal sealed partial class MgrHeatAbnormalWaveVisual : ColorRect
{
    private static Shader? _shader;
    private readonly float _intensity;
    private float _age;
    private ShaderMaterial? _shaderMaterial;

    public MgrHeatAbnormalWaveVisual(float intensity)
    {
        _intensity = Mathf.Clamp(
            intensity,
            MgrVisualTuning.HeatAbnormalVfx.MinimumIntensity,
            MgrVisualTuning.HeatAbnormalVfx.MaximumIntensity);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        Color = Colors.White;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        _shaderMaterial = new ShaderMaterial
        {
            Shader = GetShader()
        };
        Material = _shaderMaterial;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        float lifetime = MgrVisualTuning.HeatAbnormalVfx.LifetimeSeconds;
        if (_age >= lifetime)
        {
            GetParent()?.QueueFree();
            return;
        }

        float progress = Math.Clamp(_age / lifetime, 0f, 1f);
        _shaderMaterial?.SetShaderParameter("progress", progress);
        _shaderMaterial?.SetShaderParameter("intensity", _intensity);
        _shaderMaterial?.SetShaderParameter(
            "distortion_strength",
            MgrVisualTuning.HeatAbnormalVfx.DistortionStrength);
    }

    private static Shader GetShader() => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform float progress : hint_range(0.0, 1.0) = 0.0;
            uniform float intensity : hint_range(0.0, 2.0) = 0.72;
            uniform float distortion_strength = 0.0062;

            float hash(vec2 point) {
                return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453);
            }

            float noise(vec2 point) {
                vec2 cell = floor(point);
                vec2 fraction = fract(point);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float a = hash(cell);
                float b = hash(cell + vec2(1.0, 0.0));
                float c = hash(cell + vec2(0.0, 1.0));
                float d = hash(cell + vec2(1.0, 1.0));
                return mix(mix(a, b, fraction.x), mix(c, d, fraction.x), fraction.y);
            }

            void fragment() {
                vec2 uv = SCREEN_UV;
                float eased_progress = 1.0 - pow(1.0 - progress, 1.18);
                float front = mix(1.24, -0.22, eased_progress);
                float large_roll = sin(uv.x * 12.5 + TIME * 3.1) * 0.024;
                float small_roll = sin(uv.x * 31.0 - TIME * 5.4) * 0.009;
                float broken_edge = (noise(vec2(uv.x * 15.0, TIME * 0.9)) - 0.5) * 0.018;
                float distance_to_front = uv.y - front - large_roll - small_roll - broken_edge;

                // Positive distance is the heated wake below the rising crest.
                // Keeping it broad but finite reads as hot air rather than an
                // opaque wall travelling over the battlefield.
                float wake_entry = smoothstep(-0.025, 0.050, distance_to_front);
                float wake_exit = 1.0 - smoothstep(0.10, 0.62, distance_to_front);
                float heated_wake = wake_entry * wake_exit;
                float crest = 1.0 - smoothstep(0.006, 0.055, abs(distance_to_front));
                float second_ridge = 1.0 - smoothstep(
                    0.008,
                    0.044,
                    abs(distance_to_front - 0.105 - sin(uv.x * 24.0 + TIME * 2.2) * 0.010));

                float enter = smoothstep(0.0, 0.09, progress);
                float leave = 1.0 - smoothstep(0.82, 1.0, progress);
                float envelope = enter * leave;
                float thermal_field = clamp(
                    heated_wake * 0.62 + crest + second_ridge * 0.38,
                    0.0,
                    1.0);
                float shimmer = sin(
                    uv.x * 74.0 - TIME * 10.0 +
                    noise(uv * vec2(18.0, 9.0) + vec2(0.0, TIME * 0.8)) * 5.0);
                float vertical_flutter = sin(
                    uv.x * 27.0 + uv.y * 18.0 + TIME * 7.0);
                vec2 distortion = vec2(
                    shimmer * 0.48 + large_roll * 5.0,
                    vertical_flutter);
                distortion *= distortion_strength * thermal_field * envelope * intensity;

                vec2 sampled_uv = clamp(
                    uv + distortion,
                    SCREEN_PIXEL_SIZE * 1.5,
                    vec2(1.0) - SCREEN_PIXEL_SIZE * 1.5);
                vec4 source = texture(screen_texture, sampled_uv);

                float turbulence = 0.55 + 0.45 * noise(
                    uv * vec2(11.0, 16.0) + vec2(TIME * 0.55, -TIME * 0.9));
                float warmth = heated_wake * envelope * intensity *
                    (0.035 + turbulence * 0.035);
                vec3 warm_tint = vec3(0.98, 0.31, 0.055);
                vec3 color = mix(source.rgb, source.rgb * 0.92 + warm_tint * 0.20, warmth);

                float crest_glow = (crest * 0.095 + second_ridge * 0.042) *
                    envelope * intensity;
                color += vec3(1.0, 0.43, 0.08) * crest_glow;
                COLOR = vec4(clamp(color, vec3(0.0), vec3(1.0)), source.a);
            }
            """
    };
}
