using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Mind Mirage's short-lived, texture-free ocean sweep. The post-process is
/// inserted before GlobalUi's overlay stack so played cards and popups remain
/// crisp above the water while the battlefield bends beneath it.
/// </summary>
public static class MgrMindMirageWaveVfx
{
    public static async Task Play(CardModel sourceCard)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(sourceCard.Owner) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        var waveUnderlay = new Control
        {
            Name = "MgrMindMirageWaveUnderlay",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        waveUnderlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        waveUnderlay.AddChild(new BackBufferCopy
        {
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        });
        waveUnderlay.AddChild(new MgrMindMirageWaveVisual());
        globalUi.AddChildSafely(waveUnderlay);
        globalUi.MoveChild(
            waveUnderlay,
            Math.Max(0, globalUi.Overlays.GetIndex()));

        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.MindMirageVfx.EntryBeatSeconds));
    }
}

internal sealed partial class MgrMindMirageWaveVisual : ColorRect
{
    private static Shader? _shader;
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
        float lifetime = MgrVisualTuning.MindMirageVfx.LifetimeSeconds;
        if (_age >= lifetime)
        {
            GetParent()?.QueueFree();
            return;
        }

        float progress = Math.Clamp(_age / lifetime, 0f, 1f);
        float envelope = MathF.Pow(MathF.Sin(progress * MathF.PI), 0.62f);
        _shaderMaterial?.SetShaderParameter("progress", progress);
        _shaderMaterial?.SetShaderParameter(
            "strength",
            envelope * MgrVisualTuning.MindMirageVfx.DistortionStrength);
    }

    private static Shader GetShader() => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform float progress : hint_range(0.0, 1.0) = 0.0;
            uniform float strength : hint_range(0.0, 1.0) = 0.0;

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
                // Ease out of the left edge so the awaited opening beat already
                // reveals the crest instead of pausing on an empty screen.
                float eased_progress = 1.0 - pow(1.0 - progress, 1.35);
                float front = mix(-0.32, 1.36, eased_progress);

                float large_roll = sin(uv.y * 10.5 + TIME * 2.9) * 0.030;
                float small_roll = sin(uv.y * 27.0 - TIME * 4.2) * 0.011;
                float broken_edge = (noise(vec2(uv.y * 12.0, TIME * 0.7)) - 0.5) * 0.020;
                float distance_to_front = uv.x - front - large_roll - small_roll - broken_edge;

                // A broad trailing body makes the pass read as a mass of water,
                // while three uneven ridges supply the rolling white-water edge.
                float water_body = 1.0 - smoothstep(
                    0.035,
                    0.38,
                    abs(distance_to_front + 0.17));
                float crest_main = 1.0 - smoothstep(
                    0.007,
                    0.040,
                    abs(distance_to_front));
                float crest_second = 1.0 - smoothstep(
                    0.006,
                    0.030,
                    abs(distance_to_front + 0.115 + sin(uv.y * 18.0) * 0.009));
                float crest_third = 1.0 - smoothstep(
                    0.005,
                    0.024,
                    abs(distance_to_front + 0.235 + sin(uv.y * 23.0 - TIME * 2.0) * 0.008));

                float chop = 0.48 + 0.52 * noise(vec2(uv.y * 54.0, TIME * 2.1));
                float foam = clamp(
                    crest_main * (0.72 + chop * 0.28) +
                    crest_second * 0.64 * chop +
                    crest_third * 0.38,
                    0.0,
                    1.0);

                float ripple = sin(
                    uv.y * 38.0 + uv.x * 15.0 - TIME * 7.0 +
                    noise(uv * 9.0) * 3.5);
                vec2 distortion = vec2(
                    large_roll * 0.34 + ripple * 0.0025,
                    cos(uv.x * 17.0 - uv.y * 22.0 + TIME * 5.0) * 0.0065);
                distortion *= water_body * strength;
                vec2 sampled_uv = clamp(
                    uv + distortion,
                    SCREEN_PIXEL_SIZE * 1.5,
                    vec2(1.0) - SCREEN_PIXEL_SIZE * 1.5);
                vec4 source = texture(screen_texture, sampled_uv);

                float depth = clamp(
                    0.34 + 0.38 * noise(uv * vec2(7.0, 12.0) + vec2(TIME * 0.35, 0.0)) +
                    ripple * 0.10,
                    0.0,
                    1.0);
                vec3 deep_blue = vec3(0.025, 0.17, 0.34);
                vec3 sea_green = vec3(0.10, 0.55, 0.64);
                vec3 water_tint = mix(deep_blue, sea_green, depth);
                float body_alpha = water_body * strength * (0.42 + depth * 0.12);
                vec3 color = mix(source.rgb, source.rgb * 0.62 + water_tint * 0.55, body_alpha);

                vec3 foam_color = mix(vec3(0.66, 0.91, 1.0), vec3(0.96, 1.0, 1.0), chop);
                color = mix(color, foam_color, foam * strength * 0.82);
                float glint = max(0.0, ripple) * water_body * strength * 0.09;
                color += vec3(0.35, 0.74, 0.86) * glint;
                COLOR = vec4(clamp(color, vec3(0.0), vec3(1.0)), source.a);
            }
            """
    };
}
