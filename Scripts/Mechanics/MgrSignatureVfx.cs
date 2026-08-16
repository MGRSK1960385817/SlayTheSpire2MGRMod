using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Card-specific, texture-free VFX that do not belong to the shared Note or
/// Performance UI. Gameplay timing stays in the card; these nodes own only
/// presentation and always free themselves.
/// </summary>
public static class MgrSignatureVfx
{
    public static Node2D? CreateStageSpotlight(
        Creature target,
        bool empowered)
    {
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (TestMode.IsOn || targetNode is null)
            return null;

        var visual = new MgrStageSpotlightVisual();
        visual.Initialize(empowered);
        visual.GlobalPosition = targetNode.GetBottomOfHitbox();
        return visual;
    }

    public static Node2D? CreateFlowerBloom(Creature target)
    {
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (TestMode.IsOn || targetNode is null)
            return null;

        var visual = new MgrFlowerBloomVisual();
        visual.GlobalPosition = targetNode.VfxSpawnPosition;
        return visual;
    }

    public static void SpawnFallingBird(Creature target, float scale = 1f)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        NCreature? targetNode = room.GetCreatureNode(target);
        if (targetNode is null)
            return;

        var visual = new MgrFallingBirdVisual();
        visual.Initialize(scale);
        visual.GlobalPosition = targetNode.VfxSpawnPosition;
        room.CombatVfxContainer.AddChildSafely(visual);
    }

    public static async Task PlayMeteorShower(
        CardModel sourceCard,
        Creature target,
        int meteorCount)
    {
        if (meteorCount <= 0 ||
            TestMode.IsOn ||
            NCombatRoom.Instance is not { } room)
        {
            return;
        }

        NCreature? targetNode = room.GetCreatureNode(target);
        if (targetNode is null)
            return;

        var visual = new MgrMeteorShowerVisual();
        visual.Initialize(meteorCount);
        visual.GlobalPosition = targetNode.VfxSpawnPosition;
        room.CombatVfxContainer.AddChildSafely(visual);

        // Let the first meteor complete most of its flight before the native
        // multi-hit resolves. The visual itself then continues in a strictly
        // serial cadence instead of releasing overlapping clusters.
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            0.34f));
    }

    public static void PlayWhirlwindWind(
        Color tint,
        bool movingRightwards = true)
    {
        if (TestMode.IsOn)
            return;

        if (NCombatRoom.Instance is { } room)
        {
            NHorizontalLinesVfx? lines = NHorizontalLinesVfx.Create(
                tint,
                duration: 1.0,
                movingRightwards: movingRightwards);
            if (lines is not null)
                room.CombatVfxContainer.AddChildSafely(lines);
        }

        if (NRun.Instance?.GlobalUi is { } globalUi)
        {
            Color vignette = tint with { A = Math.Min(0.18f, tint.A) };
            Color highlight = tint with { A = Math.Min(0.10f, tint.A) };
            NSmokyVignetteVfx? smoke = NSmokyVignetteVfx.Create(
                vignette,
                highlight);
            if (smoke is not null)
                globalUi.AddChildSafely(smoke);
        }
    }

    public static void SpawnCelebrationStars(Creature target)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        NCreature? targetNode = room.GetCreatureNode(target);
        if (targetNode is null)
            return;

        var burst = new MgrPerformanceCardBurstVisual
        {
            FreeWhenFinished = true,
            GlobalPosition = targetNode.VfxSpawnPosition
        };
        room.CombatVfxContainer.AddChildSafely(burst);
        burst.Burst();
    }

    public static void SpawnRainbowStarRing(
        Creature target,
        int starCount = 34)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        NCreature? targetNode = room.GetCreatureNode(target);
        if (targetNode is null)
            return;

        var visual = new MgrRainbowStarRingVisual();
        visual.Initialize(starCount);
        visual.GlobalPosition = targetNode.VfxSpawnPosition;
        room.CombatVfxContainer.AddChildSafely(visual);
    }

    public static void SpawnNightmareHands(
        Player player,
        float visibleSeconds = 2f,
        float initialAlpha = 1f)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(player) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        NNightmareHandsVfx? hands = NNightmareHandsVfx.Create();
        if (hands is null)
            return;

        hands.Modulate = Colors.White with
        {
            A = Math.Clamp(initialAlpha, 0f, 1f)
        };
        globalUi.AddChildSafely(hands);

        if (visibleSeconds >= 1.95f)
            return;

        float fadeSeconds = Math.Min(0.20f, visibleSeconds * 0.35f);
        Tween tween = hands.CreateTween();
        tween.TweenInterval(Math.Max(0.05f, visibleSeconds - fadeSeconds));
        tween.TweenProperty(hands, "modulate:a", 0f, fadeSeconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        tween.TweenCallback(Callable.From(hands.QueueFree));
    }

    public static void SpawnWashoutFlash()
    {
        if (TestMode.IsOn ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        // A translucent grey rectangle only brightens the picture; it cannot
        // remove its colour. Use a short-lived screen-texture post-process in
        // the same high overlay family as Imagine/Create instead.
        var filterLayer = new CanvasLayer
        {
            Name = "MgrWashoutFlashLayer",
            Layer = 96
        };
        filterLayer.AddChild(new BackBufferCopy
        {
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        });
        filterLayer.AddChild(new MgrWashoutFlashVisual());
        globalUi.AddChildSafely(filterLayer);
    }

    public static void SpawnWatchingEyes()
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        // The back VFX container is the same layer vanilla uses for effects
        // that must appear behind creatures instead of covering their bodies.
        room.BackCombatVfxContainer.AddChildSafely(new MgrWatchingEyesVisual());
    }
}

internal sealed partial class MgrWashoutFlashVisual : ColorRect
{
    private const float Lifetime = 0.82f;
    private static Shader? _shader;
    private float _age;
    private ShaderMaterial? _shaderMaterial;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = 48;
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
        if (_age >= Lifetime)
        {
            GetParent()?.QueueFree();
            return;
        }

        float progress = Math.Clamp(_age / Lifetime, 0f, 1f);
        float envelope = MathF.Sin(progress * MathF.PI);
        _shaderMaterial?.SetShaderParameter("strength", envelope);
    }

    private static Shader GetShader() => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
            uniform float strength : hint_range(0.0, 1.0) = 0.0;

            void fragment() {
                vec4 source = texture(screen_texture, SCREEN_UV);
                float luminance = dot(source.rgb, vec3(0.2126, 0.7152, 0.0722));
                float contrasted = clamp((luminance - 0.5) * 1.42 + 0.5, 0.0, 1.0);
                vec3 monochrome = vec3(contrasted);
                COLOR = vec4(mix(source.rgb, monochrome, strength), source.a);
            }
            """
    };
}

internal sealed partial class MgrRainbowStarRingVisual : Node2D
{
    private static readonly Color[] Palette =
    [
        new("ff6f9f"), new("ffca63"), new("8ef0bd"),
        new("6edcff"), new("a98dff"), new("f38dff")
    ];

    private readonly List<BurstStar> _stars = [];
    private float _age;
    private const float Lifetime = 0.52f;

    private sealed record BurstStar(
        float Angle,
        float Speed,
        float Delay,
        float Size,
        float Spin,
        Color Color);

    public void Initialize(int starCount)
    {
        int count = Math.Clamp(starCount, 12, 72);
        for (int index = 0; index < count; index++)
        {
            float evenAngle = index * Mathf.Tau / count;
            _stars.Add(new BurstStar(
                evenAngle + RandomRange(-0.095f, 0.095f),
                RandomRange(300f, 470f),
                RandomRange(0f, 0.045f),
                RandomRange(3.4f, 8.0f),
                RandomRange(-3.2f, 3.2f),
                Palette[index % Palette.Length]));
        }

        ZIndex = 34;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (BurstStar star in _stars)
        {
            float localAge = _age - star.Delay;
            if (localAge < 0f)
                continue;

            float progress = Math.Clamp(localAge / (Lifetime - star.Delay), 0f, 1f);
            float alpha = MathF.Pow(1f - progress, 0.72f);
            float distance = 18f + star.Speed * localAge;
            Vector2 center = Vector2.FromAngle(star.Angle) * distance;
            float rotation = star.Angle + star.Spin * localAge;
            Vector2 horizontal = Vector2.FromAngle(rotation) * star.Size;
            Vector2 vertical = Vector2.FromAngle(rotation + MathF.PI * 0.5f) * star.Size * 1.45f;
            Color glow = star.Color with { A = alpha * 0.16f };
            Color core = star.Color with { A = alpha * 0.92f };
            DrawCircle(center, star.Size * 3.4f, glow);
            DrawLine(center - horizontal, center + horizontal, core, 1.7f, true);
            DrawLine(center - vertical, center + vertical, core, 2.0f, true);
        }
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
}

internal sealed partial class MgrWatchingEyesVisual : Node2D
{
    private const float Lifetime = 1.05f;
    private float _age;

    public override void _Ready()
    {
        // BackCombatVfxContainer is already ordered behind creatures and in
        // front of the room background. A negative local Z index placed this
        // child underneath the background as well, making the eyes invisible.
        ZIndex = 0;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = Math.Clamp(_age / Lifetime, 0f, 1f);
        float openness = MathF.Sin(progress * MathF.PI);
        openness = MathF.Pow(Math.Max(0f, openness), 0.72f);
        float alpha = MathF.Sin(progress * MathF.PI) * 0.82f;
        Rect2 viewport = GetViewportRect();
        Vector2 screenCenter = viewport.Position + viewport.Size * new Vector2(0.5f, 0.42f);
        float eyeRadiusX = Math.Min(330f, viewport.Size.X * 0.17f);
        float eyeRadiusY = Math.Min(138f, viewport.Size.Y * 0.14f) * openness;
        float separation = viewport.Size.X * 0.225f;

        DrawEye(screenCenter + Vector2.Left * separation, eyeRadiusX, eyeRadiusY, alpha);
        DrawEye(screenCenter + Vector2.Right * separation, eyeRadiusX, eyeRadiusY, alpha);
    }

    private void DrawEye(Vector2 center, float radiusX, float radiusY, float alpha)
    {
        const int segments = 32;
        var outline = new Vector2[segments * 2 + 1];
        for (int index = 0; index <= segments; index++)
        {
            float normalized = index / (float)segments;
            float x = Mathf.Lerp(-radiusX, radiusX, normalized);
            float arch = MathF.Sin(normalized * MathF.PI);
            outline[index] = center + new Vector2(x, -radiusY * arch);
            outline[segments * 2 - index] = center + new Vector2(x, radiusY * arch);
        }

        Color fill = new(0.10f, 0.025f, 0.08f, alpha * 0.34f);
        DrawPolygon(outline, Enumerable.Repeat(fill, outline.Length).ToArray());
        Color rim = new(0.95f, 0.25f, 0.48f, alpha);
        DrawPolyline(outline, rim, 5.5f, true);

        float irisRadius = Math.Max(3f, radiusY * 0.52f);
        DrawCircle(center, irisRadius * 1.55f,
            new Color(0.18f, 0.02f, 0.10f, alpha * 0.78f));
        DrawCircle(center, irisRadius,
            new Color(0.93f, 0.16f, 0.33f, alpha * 0.88f));
        DrawCircle(center, irisRadius * 0.42f,
            new Color(0.015f, 0.008f, 0.022f, alpha));
        DrawCircle(center - new Vector2(irisRadius * 0.22f, irisRadius * 0.22f), irisRadius * 0.12f,
            Colors.White with { A = alpha * 0.78f });
    }
}

internal sealed partial class MgrStageSpotlightVisual : Node2D
{
    private const float Lifetime = 0.58f;
    private float _age;
    private bool _empowered;

    public void Initialize(bool empowered)
    {
        _empowered = empowered;
        ZIndex = 25;
        Scale = Vector2.One * (empowered ? 1.28f : 1f);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = Math.Clamp(_age / Lifetime, 0f, 1f);
        float envelope = MathF.Sin(progress * MathF.PI);
        int beamCount = _empowered ? 3 : 1;
        for (int index = 0; index < beamCount; index++)
        {
            float offset = (index - (beamCount - 1) * 0.5f) * 42f;
            float width = index == beamCount / 2 ? 58f : 34f;
            Color beam = index % 2 == 0
                ? new Color(1f, 0.92f, 0.58f, 0.20f * envelope)
                : new Color(0.82f, 0.72f, 1f, 0.16f * envelope);
            DrawPolygon(
                [
                    new Vector2(offset - width * 1.8f, -850f),
                    new Vector2(offset + width * 1.8f, -850f),
                    new Vector2(offset + width, 28f),
                    new Vector2(offset - width, 28f)
                ],
                new Color[] { beam, beam, beam, beam });
        }

        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1.9f, 0.42f));
        Color floorGlow = new(1f, 0.88f, 0.48f, 0.46f * envelope);
        DrawCircle(Vector2.Zero, _empowered ? 62f : 48f, floorGlow);
        DrawArc(
            Vector2.Zero,
            _empowered ? 78f : 60f,
            0f,
            Mathf.Tau,
            48,
            Colors.White with { A = 0.86f * envelope },
            _empowered ? 4.6f : 3.2f,
            true);
        DrawSetTransform(Vector2.Zero);

        int sparkCount = _empowered ? 18 : 10;
        for (int index = 0; index < sparkCount; index++)
        {
            float angle = index * Mathf.Tau / sparkCount + progress * 0.7f;
            float radius = Mathf.Lerp(22f, _empowered ? 104f : 76f, progress);
            Vector2 center = Vector2.FromAngle(angle) * radius;
            Color color = index % 3 == 0
                ? new Color(0.88f, 0.74f, 1f, envelope)
                : new Color(1f, 0.93f, 0.64f, envelope);
            float size = _empowered ? 5.5f : 4f;
            DrawLine(center - Vector2.Right * size, center + Vector2.Right * size, color, 1.5f, true);
            DrawLine(center - Vector2.Up * size, center + Vector2.Up * size, color, 1.5f, true);
        }
    }
}

internal sealed partial class MgrFlowerBloomVisual : Node2D
{
    private const float Lifetime = 0.52f;
    private float _age;

    public override void _Ready()
    {
        ZIndex = 28;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = Math.Clamp(_age / Lifetime, 0f, 1f);
        float bloom = 1f - MathF.Pow(1f - progress, 3f);
        float alpha = MathF.Sin(progress * MathF.PI);
        int petals = 12;
        for (int index = 0; index < petals; index++)
        {
            float angle = index * Mathf.Tau / petals + progress * 0.28f;
            Vector2 direction = Vector2.FromAngle(angle);
            Vector2 center = direction * Mathf.Lerp(8f, 72f, bloom);
            Vector2 tangent = new(-direction.Y, direction.X);
            float length = Mathf.Lerp(10f, 30f, bloom);
            float width = Mathf.Lerp(5f, 13f, bloom);
            Color petal = (index % 3) switch
            {
                0 => new Color(1f, 0.52f, 0.72f, 0.78f * alpha),
                1 => new Color(0.86f, 0.68f, 1f, 0.72f * alpha),
                _ => new Color(1f, 0.91f, 0.68f, 0.76f * alpha)
            };
            DrawPolygon(
                [
                    center - tangent * width,
                    center + direction * length,
                    center + tangent * width,
                    center - direction * length * 0.28f
                ],
                new Color[] { petal, petal, petal, petal });
        }

        DrawCircle(Vector2.Zero, Mathf.Lerp(8f, 24f, bloom),
            Colors.White with { A = 0.62f * alpha });
        DrawArc(Vector2.Zero, Mathf.Lerp(18f, 92f, bloom), 0f, Mathf.Tau, 48,
            new Color(1f, 0.75f, 0.88f, 0.58f * alpha), 3f, true);
    }
}

internal sealed partial class MgrFallingBirdVisual : Node2D
{
    private const float Lifetime = 0.62f;
    private float _age;
    private float _effectScale = 1f;

    public void Initialize(float scale)
    {
        _effectScale = Math.Max(0.1f, scale);
        ZIndex = 32;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = Math.Clamp(_age / Lifetime, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        Vector2 position = new Vector2(-190f, -570f).Lerp(Vector2.Zero, eased);
        float alpha = progress < 0.70f ? 0.78f : (1f - progress) / 0.30f * 0.78f;
        float wingBeat = MathF.Sin(progress * MathF.PI * 5f) * 10f;
        float size = 42f * _effectScale;
        Color shadow = new(0.06f, 0.04f, 0.12f, alpha);
        Color rim = new(0.86f, 0.74f, 1f, alpha * 0.82f);

        DrawSetTransform(position, progress * 0.12f, Vector2.One);
        DrawPolygon(
            [
                new Vector2(-size * 1.35f, wingBeat),
                new Vector2(-size * 0.28f, -size * 0.18f),
                new Vector2(0f, size * 0.24f),
                new Vector2(size * 0.28f, -size * 0.18f),
                new Vector2(size * 1.35f, wingBeat),
                new Vector2(size * 0.38f, size * 0.36f),
                new Vector2(0f, size * 0.18f),
                new Vector2(-size * 0.38f, size * 0.36f)
            ],
            new Color[] { shadow, shadow, shadow, shadow, shadow, shadow, shadow, shadow });
        DrawPolyline(
            [
                new Vector2(-size * 1.35f, wingBeat),
                new Vector2(-size * 0.28f, -size * 0.18f),
                new Vector2(0f, size * 0.24f),
                new Vector2(size * 0.28f, -size * 0.18f),
                new Vector2(size * 1.35f, wingBeat)
            ],
            rim,
            2.4f,
            true);
        DrawSetTransform(Vector2.Zero);
    }
}

internal sealed partial class MgrMeteorShowerVisual : Node2D
{
    private readonly List<Meteor> _meteors = [];
    private float _age;
    private float _lifetime;

    private sealed record Meteor(
        float Delay,
        float Duration,
        Vector2 Start,
        Vector2 End,
        float Size,
        Color Color);

    public void Initialize(int count)
    {
        int meteorCount = Math.Clamp(count, 1, 40);
        float nextStart = 0f;
        for (int index = 0; index < meteorCount; index++)
        {
            // Meteors start before the previous flight has finished. Their
            // start-to-start interval is independent from damage timing and
            // gives the visual the overlapping cadence of a meteor shower.
            float delay = nextStart;
            float duration = RandomRange(0.30f, 0.38f);
            Vector2 end = new(
                RandomRange(-48f, 48f),
                RandomRange(-18f, 34f));
            Vector2 start = end + new Vector2(
                RandomRange(-430f, -285f),
                RandomRange(-680f, -485f));
            Color color = (index % 3) switch
            {
                0 => new Color("e9c5ff"),
                1 => new Color("a7dfff"),
                _ => new Color("fff0a8")
            };
            _meteors.Add(new Meteor(
                delay,
                duration,
                start,
                end,
                RandomRange(6.2f, 10.2f),
                color));
            nextStart += RandomRange(
                MgrVisualTuning.MeteorShowerVfx.SpawnIntervalMinSeconds,
                MgrVisualTuning.MeteorShowerVfx.SpawnIntervalMaxSeconds);
            _lifetime = Math.Max(_lifetime, delay + duration + 0.18f);
        }

        ZIndex = 34;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (Meteor meteor in _meteors)
        {
            float localAge = _age - meteor.Delay;
            if (localAge < 0f)
                continue;

            if (localAge <= meteor.Duration)
            {
                float progress = Math.Clamp(localAge / meteor.Duration, 0f, 1f);
                float eased = progress * progress * (3f - 2f * progress);
                Vector2 position = meteor.Start.Lerp(meteor.End, eased);
                Vector2 direction = (meteor.End - meteor.Start).Normalized();
                float alpha = MathF.Sin(progress * MathF.PI) * 0.92f;
                Color glow = meteor.Color with { A = alpha * 0.25f };
                Color core = meteor.Color with { A = alpha };
                DrawLine(
                    position - direction * meteor.Size * 10f,
                    position,
                    glow,
                    meteor.Size * 3.2f,
                    true);
                DrawLine(
                    position - direction * meteor.Size * 7f,
                    position,
                    core,
                    meteor.Size * 0.8f,
                    true);
                DrawCircle(position, meteor.Size, Colors.White with { A = alpha });
            }
            else
            {
                float impactAge = localAge - meteor.Duration;
                if (impactAge > 0.15f)
                    continue;

                float impact = impactAge / 0.15f;
                float alpha = 1f - impact;
                float radius = Mathf.Lerp(4f, 34f, impact);
                DrawArc(meteor.End, radius, 0f, Mathf.Tau, 24,
                    meteor.Color with { A = alpha * 0.78f }, 2.6f, true);
            }
        }
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
}
