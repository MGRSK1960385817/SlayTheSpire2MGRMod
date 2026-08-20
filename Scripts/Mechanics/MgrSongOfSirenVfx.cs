using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// A light, top-to-bottom sinking refraction for Song of Siren. Only its short
/// opening beat blocks gameplay; the remaining tail overlaps block and
/// Strength loss so a manual play can reach Performance sooner. Both durations
/// retain the ordinary Performance replay compression.
/// </summary>
public static class MgrSongOfSirenVfx
{
    public static async Task Play(CardModel sourceCard)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(sourceCard.Owner) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        float visualDuration = MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.SongOfSirenVfx.LifetimeSeconds);
        float impactBeat = MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.SongOfSirenVfx.ImpactBeatSeconds);
        var underlay = new Control
        {
            Name = "MgrSongOfSirenUnderlay",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        underlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        underlay.AddChild(new BackBufferCopy
        {
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        });
        underlay.AddChild(new MgrSongOfSirenSinkVisual(visualDuration));
        globalUi.AddChildSafely(underlay);
        globalUi.MoveChild(
            underlay,
            Math.Max(0, globalUi.Overlays.GetIndex()));

        await Cmd.Wait(Math.Min(impactBeat, visualDuration));
    }
}

internal sealed partial class MgrSongOfSirenSinkVisual(
    float lifetimeSeconds) : ColorRect
{
    private static Shader? _shader;
    private readonly float _lifetimeSeconds = Math.Max(0.05f, lifetimeSeconds);
    private float _age;
    private ShaderMaterial? _shaderMaterial;

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
        if (_age >= _lifetimeSeconds)
        {
            GetParent()?.QueueFree();
            return;
        }

        float progress = Math.Clamp(_age / _lifetimeSeconds, 0f, 1f);
        _shaderMaterial?.SetShaderParameter("progress", progress);
        _shaderMaterial?.SetShaderParameter(
            "distortion_strength",
            MgrVisualTuning.SongOfSirenVfx.DistortionStrength);
        _shaderMaterial?.SetShaderParameter(
            "tint_strength",
            MgrVisualTuning.SongOfSirenVfx.TintStrength);
    }

    private static Shader GetShader() => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform float progress : hint_range(0.0, 1.0) = 0.0;
            uniform float distortion_strength = 0.0032;
            uniform float tint_strength = 0.14;

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
                float eased_progress = progress * progress * (3.0 - 2.0 * progress);
                float front = mix(-0.18, 1.18, eased_progress);
                float broad_roll = sin(uv.x * 11.5 + TIME * 2.6) * 0.022;
                float fine_roll = sin(uv.x * 29.0 - TIME * 4.1) * 0.008;
                float broken_edge = (noise(vec2(uv.x * 13.0, TIME * 0.62)) - 0.5) * 0.015;
                float distance_to_front = uv.y - front - broad_roll - fine_roll - broken_edge;

                // The wake remains above the descending front. Sampling from
                // slightly higher UVs makes the captured battlefield appear
                // to sag downward behind it, like a scene sinking under water.
                float wake_entry = smoothstep(-0.60, -0.08, distance_to_front);
                float wake_exit = 1.0 - smoothstep(-0.025, 0.12, distance_to_front);
                float sinking_wake = wake_entry * wake_exit;
                float crest = 1.0 - smoothstep(0.006, 0.050, abs(distance_to_front));
                float echo_one = 1.0 - smoothstep(
                    0.006,
                    0.038,
                    abs(distance_to_front + 0.105 + sin(uv.x * 18.0 + TIME * 1.8) * 0.008));
                float echo_two = 1.0 - smoothstep(
                    0.005,
                    0.030,
                    abs(distance_to_front + 0.205 + sin(uv.x * 25.0 - TIME * 2.1) * 0.006));

                float enter = smoothstep(0.0, 0.08, progress);
                float leave = 1.0 - smoothstep(0.88, 1.0, progress);
                float envelope = enter * leave;
                float field = clamp(
                    sinking_wake * 0.52 + crest + echo_one * 0.42 + echo_two * 0.24,
                    0.0,
                    1.0);
                float wavering = sin(
                    uv.x * 41.0 + uv.y * 13.0 - TIME * 5.2 +
                    noise(uv * vec2(12.0, 8.0)) * 3.2);
                float side_pull = sin(uv.y * 22.0 + TIME * 3.4) * 0.38;
                vec2 distortion = vec2(side_pull, -0.76 + wavering * 0.24);
                distortion *= distortion_strength * field * envelope;

                vec2 sampled_uv = clamp(
                    uv + distortion,
                    SCREEN_PIXEL_SIZE * 1.5,
                    vec2(1.0) - SCREEN_PIXEL_SIZE * 1.5);
                vec4 source = texture(screen_texture, sampled_uv);

                float depth_noise = 0.58 + 0.42 * noise(
                    uv * vec2(8.0, 13.0) + vec2(TIME * 0.22, -TIME * 0.36));
                float body = sinking_wake * envelope * tint_strength *
                    (0.62 + depth_noise * 0.38);
                vec3 deep_blue = vec3(0.035, 0.11, 0.22);
                vec3 siren_violet = vec3(0.24, 0.13, 0.39);
                vec3 tint = mix(deep_blue, siren_violet, depth_noise);
                vec3 color = mix(source.rgb, source.rgb * 0.88 + tint * 0.22, body);

                float ridge_light = (crest * 0.055 + echo_one * 0.030 + echo_two * 0.016) * envelope;
                color += vec3(0.30, 0.58, 0.82) * ridge_light;
                COLOR = vec4(clamp(color, vec3(0.0), vec3(1.0)), source.a);
            }
            """
    };
}
