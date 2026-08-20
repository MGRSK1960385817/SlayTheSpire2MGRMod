using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Hyakki Yagyo's texture-free anticipation and target-local impact. The
/// screen sample remains below GlobalUi overlays, while damage VFX stay in the
/// ordinary combat VFX container with the creatures they describe.
/// </summary>
public static class MgrHyakkiYagyoVfx
{
    public static async Task PlayPrelude(CardModel sourceCard)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(sourceCard.Owner) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        var underlay = new Control
        {
            Name = "MgrHyakkiYagyoUnderlay",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        underlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        underlay.AddChild(new BackBufferCopy
        {
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        });
        underlay.AddChild(new MgrHyakkiYagyoPreludeVisual());
        globalUi.AddChildSafely(underlay);
        globalUi.MoveChild(
            underlay,
            Math.Max(0, globalUi.Overlays.GetIndex()));

        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.HyakkiYagyoVfx.ImpactBeatSeconds));
    }

    public static Node2D? CreateImpact(
        Creature target,
        Color fireTint,
        float fireScale)
    {
        if (TestMode.IsOn ||
            NCombatRoom.Instance is not { } room ||
            room.GetCreatureNode(target) is not { } creatureNode)
        {
            return null;
        }

        // The zero-position group lets its two children retain their separate
        // screen-space anchors: ripples use the hit centre, while the native
        // fire remains rooted at the bottom of the creature hitbox.
        var group = new MgrHyakkiYagyoImpactGroup();
        group.AddChild(new MgrHyakkiYagyoRippleVisual
        {
            Position = creatureNode.VfxSpawnPosition
        });

        if (MgrAttackVfx.CreateFireBurst(target, fireTint, fireScale) is
            { } fire)
        {
            group.AddChild(fire);
        }

        return group;
    }
}

internal sealed partial class MgrHyakkiYagyoPreludeVisual : ColorRect
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
        float lifetime = MgrVisualTuning.HyakkiYagyoVfx.PreludeLifetimeSeconds;
        if (_age >= lifetime)
        {
            GetParent()?.QueueFree();
            return;
        }

        float progress = Math.Clamp(_age / lifetime, 0f, 1f);
        _shaderMaterial?.SetShaderParameter("progress", progress);
        _shaderMaterial?.SetShaderParameter(
            "maximum_darkness",
            MgrVisualTuning.HyakkiYagyoVfx.MaximumDarkness);
        _shaderMaterial?.SetShaderParameter(
            "distortion_strength",
            MgrVisualTuning.HyakkiYagyoVfx.DistortionStrength);
    }

    private static Shader GetShader() => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform float progress : hint_range(0.0, 1.0) = 0.0;
            uniform float maximum_darkness = 0.40;
            uniform float distortion_strength = 0.0038;

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
                float rise = smoothstep(0.0, 0.42, progress);
                float release = 1.0 - smoothstep(0.48, 1.0, progress);
                float darkness = rise * release;

                // Distortion enters after the first darkening beat and breaks
                // into several shallow, offset rings before the impact.
                float distortion_entry = smoothstep(0.16, 0.46, progress);
                float distortion_envelope = distortion_entry * release;
                vec2 centred = uv - vec2(0.5);
                centred.x *= SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
                float radius = length(centred);
                vec2 direction = centred / max(radius, 0.001);
                float radial_wave = sin(radius * 55.0 - progress * 15.0);
                float broken_wave = noise(uv * vec2(8.0, 12.0) + vec2(progress * 3.0, 0.0)) - 0.5;
                vec2 tangent = vec2(-direction.y, direction.x);
                vec2 offset = direction * radial_wave;
                offset += tangent * broken_wave * 0.65;
                offset *= distortion_strength * distortion_envelope;

                vec2 sampled_uv = clamp(
                    uv + offset,
                    SCREEN_PIXEL_SIZE * 1.5,
                    vec2(1.0) - SCREEN_PIXEL_SIZE * 1.5);
                vec4 source = texture(screen_texture, sampled_uv);
                vec3 shadow_tint = vec3(0.055, 0.008, 0.026);
                vec3 darkened = source.rgb * (1.0 - maximum_darkness * darkness);
                darkened += shadow_tint * darkness * 0.18;

                // A faint wine-coloured interference line keeps the otherwise
                // near-black distortion readable on already dark encounters.
                float interference = pow(max(0.0, radial_wave), 8.0) *
                    distortion_envelope * 0.055;
                darkened += vec3(0.30, 0.018, 0.075) * interference;
                COLOR = vec4(clamp(darkened, vec3(0.0), vec3(1.0)), source.a);
            }
            """
    };
}

internal sealed partial class MgrHyakkiYagyoImpactGroup : Node2D
{
    private float _age;

    public override void _Ready()
    {
        ZIndex = MgrVisualTuning.HyakkiYagyoVfx.ImpactZIndex;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= MgrVisualTuning.HyakkiYagyoVfx.ImpactGroupLifetimeSeconds)
            QueueFree();
    }
}

internal sealed partial class MgrHyakkiYagyoRippleVisual : Node2D
{
    private static readonly Color Ink = new(0.008f, 0.002f, 0.012f, 1f);
    private static readonly Color Wine = new(0.31f, 0.018f, 0.075f, 1f);
    private float _age;

    public override void _Ready()
    {
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= MgrVisualTuning.HyakkiYagyoVfx.ImpactLifetimeSeconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float lifetime = MgrVisualTuning.HyakkiYagyoVfx.ImpactLifetimeSeconds;
        float progress = Math.Clamp(_age / lifetime, 0f, 1f);
        float fade = MathF.Pow(1f - progress, 1.45f);
        float opening = MathF.Sin(Math.Min(1f, progress * 2.2f) * MathF.PI * 0.5f);

        DrawCircle(
            Vector2.Zero,
            29f + 14f * opening,
            Ink with { A = 0.42f * fade });
        DrawCircle(
            Vector2.Zero,
            18f + 8f * opening,
            Wine with { A = 0.18f * fade });

        for (int index = 0; index < 3; index++)
        {
            float delayed = Math.Clamp(progress * 1.28f - index * 0.12f, 0f, 1f);
            float eased = 1f - MathF.Pow(1f - delayed, 2.6f);
            float radius = Mathf.Lerp(
                17f + index * 7f,
                MgrVisualTuning.HyakkiYagyoVfx.MaximumRippleRadius *
                    (1f - index * 0.08f),
                eased);
            float alpha = (1f - delayed) * (0.78f - index * 0.13f);
            float phase = index * 1.73f - progress * (1.5f + index * 0.22f);

            DrawArc(
                Vector2.Zero,
                radius,
                phase,
                phase + MathF.PI * 1.42f,
                52,
                Ink with { A = alpha * fade },
                6.2f - index * 1.1f,
                true);
            DrawArc(
                Vector2.Zero,
                radius + 2.2f,
                phase + 0.18f,
                phase + MathF.PI * 1.16f,
                46,
                Wine with { A = alpha * fade * 0.72f },
                2.1f,
                true);
        }

        DrawInkFlames(progress, fade);
    }

    private void DrawInkFlames(float progress, float fade)
    {
        float rise = 1f - MathF.Pow(1f - progress, 1.7f);
        for (int index = 0; index < 7; index++)
        {
            float phase = index * 1.31f;
            float x = -54f + index * 18f + MathF.Sin(progress * 7f + phase) * 5f;
            float height = 42f + index % 3 * 13f;
            float width = 11f + index % 2 * 4f;
            float baseY = 34f + MathF.Cos(phase) * 7f;
            float tipY = baseY - height * (0.58f + rise * 0.72f);
            float sway = MathF.Sin(progress * 9f + phase) * 8f;
            Vector2[] flame =
            [
                new(x - width, baseY),
                new(x - width * 0.58f, baseY - height * 0.42f),
                new(x + sway, tipY),
                new(x + width * 0.62f, baseY - height * 0.36f),
                new(x + width, baseY)
            ];
            float alpha = fade * (0.34f + index % 3 * 0.055f);
            DrawPolygon(
                flame,
                Enumerable.Repeat(
                    Ink with { A = alpha },
                    flame.Length).ToArray());
            DrawPolyline(
                flame,
                Wine with { A = alpha * 0.72f },
                1.8f,
                true);
        }
    }
}
