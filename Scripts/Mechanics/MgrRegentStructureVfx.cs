using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Texture-free card VFX built from MGR-owned drawing and shaders. These
/// effects borrow only the anticipation/impact structure documented in the
/// RegentFX assessment; no RegentFX resource or source code is used here.
/// </summary>
public static class MgrRegentStructureVfx
{
    public static void SpawnPrismaticDistortion(Creature target)
    {
        if (!TryGetCreatureUv(target, out Vector2 centerUv))
            return;

        AddScreenUnderlay(new MgrPrismaticRingDistortionVisual(centerUv));
    }

    public static async Task PlayGalaxyLampConversion(
        CardModel sourceCard,
        Creature target,
        IReadOnlyList<NoteKind> noteKinds)
    {
        if (noteKinds.Count == 0 ||
            TestMode.IsOn ||
            NCombatRoom.Instance is not { } room ||
            room.GetCreatureNode(target) is not { } creatureNode)
        {
            return;
        }

        var visual = new MgrGalaxyLampConversionVisual(noteKinds)
        {
            GlobalPosition = creatureNode.VfxSpawnPosition
        };
        room.CombatVfxContainer.AddChildSafely(visual);
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.GalaxyLampVfx.ConvergenceSeconds));
    }

    public static async Task PlayMeteorAftermath(
        CardModel sourceCard,
        Creature target)
    {
        if (TestMode.IsOn ||
            NCombatRoom.Instance is not { } room ||
            room.GetCreatureNode(target) is not { } creatureNode)
        {
            return;
        }

        var visual = new MgrMeteorAftermathVisual
        {
            GlobalPosition = creatureNode.VfxSpawnPosition
        };
        room.CombatVfxContainer.AddChildSafely(visual);
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.MeteorAftermathVfx.ConvergenceSeconds));
    }

    public static void SpawnCubicPrismRefraction(
        Creature attacker,
        IReadOnlyList<Creature> targets,
        float intensityScale)
    {
        if (!TryGetCreatureUv(attacker, out Vector2 startUv) ||
            !TryGetAverageCreatureUv(targets, out Vector2 endUv))
        {
            return;
        }

        AddScreenUnderlay(new MgrPrismBeamRefractionVisual(
            startUv,
            endUv,
            intensityScale));
    }

    private static bool TryGetCreatureUv(
        Creature creature,
        out Vector2 uv)
    {
        uv = Vector2.Zero;
        if (TestMode.IsOn ||
            NCombatRoom.Instance is not { } room ||
            room.GetCreatureNode(creature) is not { } creatureNode)
        {
            return false;
        }

        Vector2 viewportSize = room.GetViewportRect().Size;
        if (viewportSize.X <= 1f || viewportSize.Y <= 1f)
            return false;

        Vector2 position = creatureNode.VfxSpawnPosition;
        uv = new Vector2(
            Math.Clamp(position.X / viewportSize.X, 0f, 1f),
            Math.Clamp(position.Y / viewportSize.Y, 0f, 1f));
        return true;
    }

    private static bool TryGetAverageCreatureUv(
        IReadOnlyList<Creature> creatures,
        out Vector2 uv)
    {
        uv = Vector2.Zero;
        int count = 0;
        foreach (Creature creature in creatures)
        {
            if (!TryGetCreatureUv(creature, out Vector2 creatureUv))
                continue;

            uv += creatureUv;
            count++;
        }

        if (count == 0)
            return false;

        uv /= count;
        return true;
    }

    private static void AddScreenUnderlay(ColorRect visual)
    {
        if (NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
            return;

        var underlay = new Control
        {
            Name = "MgrLocalDistortionUnderlay",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        underlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        underlay.AddChild(new BackBufferCopy
        {
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        });
        underlay.AddChild(visual);
        globalUi.AddChildSafely(underlay);
        globalUi.MoveChild(
            underlay,
            Math.Max(0, globalUi.Overlays.GetIndex()));
    }
}

internal abstract partial class MgrScreenDistortionVisual : ColorRect
{
    private float _age;
    private ShaderMaterial? _shaderMaterial;

    protected abstract float LifetimeSeconds { get; }
    protected abstract Shader EffectShader { get; }

    protected ShaderMaterial MaterialInstance =>
        _shaderMaterial ?? throw new InvalidOperationException(
            "The distortion material is not ready.");

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
            Shader = EffectShader
        };
        Material = _shaderMaterial;
        ConfigureMaterial(_shaderMaterial);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= LifetimeSeconds)
        {
            GetParent()?.QueueFree();
            return;
        }

        float progress = Math.Clamp(_age / LifetimeSeconds, 0f, 1f);
        MaterialInstance.SetShaderParameter("progress", progress);
        UpdateMaterial(MaterialInstance, progress);
    }

    protected virtual void ConfigureMaterial(ShaderMaterial material)
    {
    }

    protected virtual void UpdateMaterial(
        ShaderMaterial material,
        float progress)
    {
    }
}

internal sealed partial class MgrPrismaticRingDistortionVisual(
    Vector2 centerUv) : MgrScreenDistortionVisual
{
    private static Shader? _shader;

    protected override float LifetimeSeconds =>
        MgrVisualTuning.PrismaticVfx.RingLifetimeSeconds;

    protected override Shader EffectShader => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform vec2 center_uv = vec2(0.5);
            uniform float progress : hint_range(0.0, 1.0) = 0.0;
            uniform float strength = 0.006;
            uniform float maximum_radius = 0.19;
            uniform float phase = 0.0;

            void fragment() {
                vec2 uv = SCREEN_UV;
                float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
                vec2 delta = uv - center_uv;
                vec2 metric = delta * vec2(aspect, 1.0);
                float distance_to_center = length(metric);
                vec2 direction = distance_to_center > 0.0001
                    ? normalize(metric) / vec2(aspect, 1.0)
                    : vec2(0.0);
                vec2 tangent = vec2(-direction.y, direction.x);
                float eased = 1.0 - pow(1.0 - progress, 2.4);
                float radius = mix(0.018, maximum_radius, eased);
                float ring = 1.0 - smoothstep(0.010, 0.035, abs(distance_to_center - radius));
                float envelope = pow(sin(progress * 3.14159265), 0.72);
                float ripple = sin((distance_to_center - radius) * 150.0 + phase) * 0.55 + 0.45;
                vec2 offset = (direction * ripple + tangent * 0.34) * strength * ring * envelope;
                vec2 sample_uv = clamp(uv + offset, SCREEN_PIXEL_SIZE, vec2(1.0) - SCREEN_PIXEL_SIZE);
                float shift = strength * 0.46 * ring * envelope;
                float red = texture(screen_texture, sample_uv + direction * shift).r;
                float green = texture(screen_texture, sample_uv).g;
                float blue = texture(screen_texture, sample_uv - direction * shift).b;
                vec4 source = texture(screen_texture, uv);
                vec3 refracted = vec3(red, green, blue);
                vec3 prism_tint = vec3(0.76, 0.52, 1.0) * ring * envelope * 0.08;
                COLOR = vec4(mix(source.rgb, refracted, ring * envelope) + prism_tint, source.a);
            }
            """
    };

    protected override void ConfigureMaterial(ShaderMaterial material)
    {
        material.SetShaderParameter("center_uv", centerUv);
        material.SetShaderParameter(
            "strength",
            MgrVisualTuning.PrismaticVfx.DistortionStrength);
        material.SetShaderParameter(
            "maximum_radius",
            MgrVisualTuning.PrismaticVfx.MaximumRadius);
        material.SetShaderParameter(
            "phase",
            Random.Shared.NextSingle() * Mathf.Tau);
    }
}

internal sealed partial class MgrPrismBeamRefractionVisual(
    Vector2 startUv,
    Vector2 endUv,
    float intensityScale) : MgrScreenDistortionVisual
{
    private static Shader? _shader;

    protected override float LifetimeSeconds =>
        MgrVisualTuning.CubicPrismVfx.RefractionLifetimeSeconds;

    protected override Shader EffectShader => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform vec2 start_uv = vec2(0.25, 0.55);
            uniform vec2 end_uv = vec2(0.75, 0.50);
            uniform float progress : hint_range(0.0, 1.0) = 0.0;
            uniform float width = 0.026;
            uniform float strength = 0.0035;

            void fragment() {
                vec2 uv = SCREEN_UV;
                float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
                vec2 scale = vec2(aspect, 1.0);
                vec2 point = uv * scale;
                vec2 start = start_uv * scale;
                vec2 end = end_uv * scale;
                vec2 segment = end - start;
                float segment_length_squared = max(dot(segment, segment), 0.00001);
                float along = clamp(dot(point - start, segment) / segment_length_squared, 0.0, 1.0);
                vec2 nearest = start + segment * along;
                float distance_to_beam = length(point - nearest);
                float band = 1.0 - smoothstep(width, width * 2.5, distance_to_beam);
                float arrival = 1.0 - smoothstep(progress + 0.04, progress + 0.16, along);
                float envelope = pow(sin(progress * 3.14159265), 0.76);
                vec2 normal = normalize(vec2(-segment.y, segment.x)) / scale;
                float oscillation = sin(along * 68.0 - progress * 18.0);
                vec2 offset = normal * oscillation * strength * band * arrival * envelope;
                vec2 sample_uv = clamp(uv + offset, SCREEN_PIXEL_SIZE, vec2(1.0) - SCREEN_PIXEL_SIZE);
                float shift = strength * 0.62 * band * arrival * envelope;
                float red = texture(screen_texture, sample_uv + normal * shift).r;
                float green = texture(screen_texture, sample_uv).g;
                float blue = texture(screen_texture, sample_uv - normal * shift).b;
                vec4 source = texture(screen_texture, uv);
                vec3 refracted = vec3(red, green, blue);
                COLOR = vec4(mix(source.rgb, refracted, band * arrival * envelope), source.a);
            }
            """
    };

    protected override void ConfigureMaterial(ShaderMaterial material)
    {
        float boundedScale = Math.Clamp(MathF.Sqrt(Math.Max(0.1f, intensityScale)), 0.7f, 1.45f);
        material.SetShaderParameter("start_uv", startUv);
        material.SetShaderParameter("end_uv", endUv);
        material.SetShaderParameter(
            "width",
            MgrVisualTuning.CubicPrismVfx.RefractionWidth * boundedScale);
        material.SetShaderParameter(
            "strength",
            MgrVisualTuning.CubicPrismVfx.RefractionStrength * boundedScale);
    }
}

internal sealed partial class MgrGalaxyLampConversionVisual : Node2D
{
    private readonly List<NoteParticle> _particles = [];
    private float _age;

    private sealed record NoteParticle(
        float Angle,
        float Radius,
        float Phase,
        float Size,
        Color Color);

    public MgrGalaxyLampConversionVisual(IReadOnlyList<NoteKind> noteKinds)
    {
        int visualCount = Math.Min(noteKinds.Count, 18);
        for (int index = 0; index < visualCount; index++)
        {
            float evenAngle = index * Mathf.Tau / Math.Max(1, visualCount);
            _particles.Add(new NoteParticle(
                evenAngle + RandomRange(-0.24f, 0.24f),
                RandomRange(118f, 214f),
                RandomRange(0f, Mathf.Tau),
                RandomRange(0.78f, 1.18f),
                GetNoteColor(noteKinds[index])));
        }
    }

    public override void _Ready()
    {
        ZIndex = MgrVisualTuning.GalaxyLampVfx.ZIndex;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= MgrVisualTuning.GalaxyLampVfx.LifetimeSeconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float convergence = MgrVisualTuning.GalaxyLampVfx.ConvergenceSeconds;
        if (_age <= convergence)
        {
            float progress = Math.Clamp(_age / convergence, 0f, 1f);
            float eased = progress * progress * (3f - 2f * progress);
            foreach (NoteParticle particle in _particles)
            {
                float angle = particle.Angle + eased * (1.2f + particle.Phase * 0.08f);
                float radius = Mathf.Lerp(particle.Radius, 8f, eased);
                Vector2 center = Vector2.FromAngle(angle) * radius;
                float alpha = 0.32f + eased * 0.68f;
                DrawNoteGlyph(center, particle.Size, particle.Color with { A = alpha });
                DrawLine(
                    center,
                    center * 0.68f,
                    particle.Color with { A = alpha * 0.25f },
                    2f,
                    true);
            }

            DrawCircle(Vector2.Zero, 18f + 22f * eased,
                new Color(0.52f, 0.82f, 1f, eased * 0.18f));
            return;
        }

        float releaseDuration = Math.Max(
            0.01f,
            MgrVisualTuning.GalaxyLampVfx.LifetimeSeconds - convergence);
        float release = Math.Clamp((_age - convergence) / releaseDuration, 0f, 1f);
        float alphaEnvelope = 1f - release;
        float ringRadius = Mathf.Lerp(22f, 178f, EaseOut(release));
        DrawArc(
            Vector2.Zero,
            ringRadius,
            0f,
            Mathf.Tau,
            64,
            new Color(0.62f, 0.90f, 1f, alphaEnvelope * 0.84f),
            4.2f,
            true);
        DrawArc(
            Vector2.Zero,
            ringRadius * 0.72f,
            release * 2.1f,
            Mathf.Tau + release * 2.1f,
            52,
            new Color(0.72f, 0.50f, 1f, alphaEnvelope * 0.58f),
            2.4f,
            true);

        foreach (NoteParticle particle in _particles)
        {
            float angle = particle.Angle + particle.Phase * 0.12f + release * 0.42f;
            Vector2 center = Vector2.FromAngle(angle) * Mathf.Lerp(16f, particle.Radius * 0.76f, EaseOut(release));
            Color color = Color.FromHsv(
                (particle.Phase / Mathf.Tau + release * 0.18f) % 1f,
                0.44f,
                1f,
                alphaEnvelope * 0.92f);
            DrawStar(center, particle.Size * 6f, color);
        }
    }

    private void DrawNoteGlyph(
        Vector2 center,
        float scale,
        Color color)
    {
        DrawCircle(center + new Vector2(-4f, 6f) * scale, 5.5f * scale, color);
        DrawLine(
            center + new Vector2(1f, 6f) * scale,
            center + new Vector2(1f, -17f) * scale,
            color,
            3f * scale,
            true);
        DrawLine(
            center + new Vector2(1f, -17f) * scale,
            center + new Vector2(12f, -12f) * scale,
            color,
            3f * scale,
            true);
    }

    private void DrawStar(Vector2 center, float size, Color color)
    {
        DrawLine(center - Vector2.Right * size, center + Vector2.Right * size, color, 2f, true);
        DrawLine(center - Vector2.Up * size, center + Vector2.Up * size, color, 2f, true);
    }

    private static Color GetNoteColor(NoteKind kind) => kind switch
    {
        NoteKind.Attack => new Color("ff718f"),
        NoteKind.Skill => new Color("71e0a0"),
        NoteKind.Power => new Color("7fb7ff"),
        NoteKind.Status => new Color("c8cedc"),
        NoteKind.Curse => new Color("bf7cff"),
        NoteKind.Starry => new Color("ffe48a"),
        _ => new Color("e5d7ff")
    };

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());

    private static float EaseOut(float value) =>
        1f - MathF.Pow(1f - value, 3f);
}

internal sealed partial class MgrMeteorAftermathVisual : Node2D
{
    private readonly List<AftermathShard> _shards = [];
    private float _age;

    private sealed record AftermathShard(
        float Angle,
        float Radius,
        float Length,
        float Phase,
        Color Color);

    public MgrMeteorAftermathVisual()
    {
        int count = MgrVisualTuning.MeteorAftermathVfx.ShardCount;
        for (int index = 0; index < count; index++)
        {
            _shards.Add(new AftermathShard(
                index * Mathf.Tau / count + RandomRange(-0.12f, 0.12f),
                RandomRange(120f, 260f),
                RandomRange(18f, 52f),
                RandomRange(0f, Mathf.Tau),
                (index % 3) switch
                {
                    0 => new Color("a789ff"),
                    1 => new Color("7fd9ff"),
                    _ => new Color("ffd58a")
                }));
        }
    }

    public override void _Ready()
    {
        ZIndex = MgrVisualTuning.MeteorAftermathVfx.ZIndex;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= MgrVisualTuning.MeteorAftermathVfx.LifetimeSeconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float convergence = MgrVisualTuning.MeteorAftermathVfx.ConvergenceSeconds;
        if (_age <= convergence)
        {
            float progress = Math.Clamp(_age / convergence, 0f, 1f);
            float eased = progress * progress;
            foreach (AftermathShard shard in _shards)
            {
                Vector2 direction = Vector2.FromAngle(shard.Angle + eased * 0.55f);
                Vector2 center = direction * Mathf.Lerp(shard.Radius, 14f, eased);
                Color color = shard.Color with { A = 0.26f + progress * 0.70f };
                DrawLine(
                    center + direction * shard.Length * 0.48f,
                    center - direction * shard.Length * 0.48f,
                    color,
                    2.6f,
                    true);
            }

            DrawCircle(Vector2.Zero, 20f + 28f * eased,
                new Color(0.54f, 0.33f, 1f, eased * 0.22f));
            return;
        }

        float releaseDuration = Math.Max(
            0.01f,
            MgrVisualTuning.MeteorAftermathVfx.LifetimeSeconds - convergence);
        float release = Math.Clamp((_age - convergence) / releaseDuration, 0f, 1f);
        float envelope = 1f - release;
        float radius = Mathf.Lerp(28f, 232f, 1f - MathF.Pow(1f - release, 3f));
        DrawArc(
            Vector2.Zero,
            radius,
            0f,
            Mathf.Tau,
            72,
            new Color(0.55f, 0.35f, 1f, envelope * 0.82f),
            5f,
            true);
        DrawArc(
            Vector2.Zero,
            radius * 0.78f,
            -release * 1.4f,
            Mathf.Tau - release * 1.4f,
            64,
            new Color(0.43f, 0.82f, 1f, envelope * 0.50f),
            2.4f,
            true);

        foreach (AftermathShard shard in _shards)
        {
            Vector2 direction = Vector2.FromAngle(shard.Angle + shard.Phase * 0.035f);
            Vector2 center = direction * Mathf.Lerp(18f, shard.Radius * 0.92f, release);
            DrawLine(
                center - direction * shard.Length * 0.38f,
                center + direction * shard.Length * 0.38f,
                shard.Color with { A = envelope * 0.72f },
                2.2f,
                true);
        }
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
}
