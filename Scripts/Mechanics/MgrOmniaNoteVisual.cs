using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Animated artwork for the Omnia Note. The shape cycles through the five
/// basic Notes and the Starry Note while a shader continuously moves a rainbow
/// across the currently displayed silhouette.
/// </summary>
public partial class MgrOmniaNoteVisual : Sprite2D
{
    private static Shader? _rainbowShader;

    private static readonly NoteKind[] ShapeKinds =
    [
        NoteKind.Attack,
        NoteKind.Skill,
        NoteKind.Power,
        NoteKind.Status,
        NoteKind.Curse,
        NoteKind.Starry
    ];

    private readonly List<Texture2D> _textures = [];
    private readonly List<OmniaSpark> _sparks = [];
    private double _shapeElapsed;
    private float _sparkElapsed;
    private int _shapeIndex;

    public bool Initialize()
    {
        foreach (NoteKind kind in ShapeKinds)
        {
            string path = $"{Entry.ResPath}/images/notes/{kind}.png";
            if (ResourceLoader.Load<Texture2D>(path) is not { } texture)
            {
                Entry.Logger.Warn($"Missing Omnia Note component texture: {path}");
                continue;
            }

            _textures.Add(texture);
        }

        if (_textures.Count == 0)
            return false;

        _shapeIndex = Random.Shared.Next(0, _textures.Count);
        Texture = _textures[_shapeIndex];
        FitCurrentTextureToDisplaySize();
        Material = CreateRainbowMaterial(Texture);
        SetProcess(true);
        return true;
    }

    public override void _Process(double delta)
    {
        if (_textures.Count > 1)
        {
            _shapeElapsed += delta;
            while (_shapeElapsed >= MgrVisualTuning.Notes.OmniaNoteShapeSeconds)
            {
                _shapeElapsed -= MgrVisualTuning.Notes.OmniaNoteShapeSeconds;
                _shapeIndex = (_shapeIndex + 1) % _textures.Count;
                Texture = _textures[_shapeIndex];
                if (Material is ShaderMaterial material)
                    material.SetShaderParameter("note_texture", Texture);
                FitCurrentTextureToDisplaySize();
            }
        }

        float seconds = (float)delta;
        _sparkElapsed += seconds;
        while (_sparkElapsed >= MgrVisualTuning.Notes.OmniaNoteSparkSeconds)
        {
            _sparkElapsed -= MgrVisualTuning.Notes.OmniaNoteSparkSeconds;
            SpawnSpark();
        }

        for (int index = _sparks.Count - 1; index >= 0; index--)
        {
            OmniaSpark spark = _sparks[index];
            spark.Age += seconds;
            spark.Position += spark.Velocity * seconds;
            spark.Rotation += spark.Spin * seconds;
            if (spark.Age >= spark.Lifetime)
                _sparks.RemoveAt(index);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (OmniaSpark spark in _sparks)
        {
            float progress = Math.Clamp(spark.Age / spark.Lifetime, 0f, 1f);
            float alpha = MathF.Sin(progress * MathF.PI) * 0.88f;
            Vector2 horizontal = Vector2.FromAngle(spark.Rotation) * spark.Size;
            Vector2 vertical = Vector2.FromAngle(spark.Rotation + MathF.PI * 0.5f) *
                spark.Size * 1.45f;
            Color glow = spark.Color with { A = alpha * 0.15f };
            Color core = spark.Color with { A = alpha };
            DrawCircle(spark.Position, spark.Size * 3.4f, glow);
            DrawLine(spark.Position - horizontal, spark.Position + horizontal, core, 5f, true);
            DrawLine(spark.Position - vertical, spark.Position + vertical, core, 5.5f, true);
        }
    }

    private void SpawnSpark()
    {
        if (_sparks.Count >= MgrVisualTuning.Notes.OmniaNoteMaximumSparks)
            _sparks.RemoveAt(0);

        float angle = Random.Shared.NextSingle() * Mathf.Tau;
        Vector2 direction = Vector2.FromAngle(angle);
        _sparks.Add(new OmniaSpark
        {
            Position = direction * RandomRange(78f, 116f),
            Velocity = direction.Rotated(RandomRange(-0.22f, 0.22f)) *
                RandomRange(62f, 118f),
            Lifetime = RandomRange(0.72f, 1.14f),
            Size = RandomRange(10f, 18f),
            Rotation = angle,
            Spin = RandomRange(-2.3f, 2.3f),
            Color = Color.FromHsv(Random.Shared.NextSingle(), 0.48f, 1f)
        });
    }

    private void FitCurrentTextureToDisplaySize()
    {
        if (Texture is null)
            return;

        Vector2 sourceSize = Texture.GetSize();
        float longestSide = MathF.Max(sourceSize.X, sourceSize.Y);
        Scale = longestSide > 0f
            ? Vector2.One *
                (MgrVisualTuning.Notes.SlotRadius * 2f *
                    MgrVisualTuning.Notes.ArtworkFillRatio / longestSide)
            : Vector2.One;
    }

    private static ShaderMaterial CreateRainbowMaterial(Texture2D noteTexture)
    {
        Shader shader = _rainbowShader ??= new Shader
        {
            Code = """
                shader_type canvas_item;

                uniform sampler2D note_texture : source_color, filter_linear, repeat_disable;
                uniform float rainbow_speed = 0.22;
                uniform float rainbow_frequency = 1.35;
                uniform float glow_radius_ratio = 0.035;
                uniform float glow_strength = 0.38;
                uniform float canvas_margin_ratio = 0.06;

                float uv_mask(vec2 uv) {
                    return step(0.0, uv.x) * step(uv.x, 1.0) *
                        step(0.0, uv.y) * step(uv.y, 1.0);
                }

                vec4 sample_source(vec2 uv) {
                    return texture(note_texture, clamp(uv, vec2(0.0), vec2(1.0))) *
                        uv_mask(uv);
                }

                void vertex() {
                    float expansion = 1.0 + canvas_margin_ratio * 2.0;
                    VERTEX *= expansion;
                    UV = (UV - vec2(0.5)) * expansion + vec2(0.5);
                }

                vec3 spectral_palette(float phase) {
                    // A softer musical palette: lavender, cyan, warm gold and
                    // rose flow into one another without harsh RGB bands.
                    vec3 center = vec3(0.80, 0.81, 0.84);
                    vec3 range = vec3(0.18, 0.17, 0.14);
                    vec3 offset = vec3(0.02, 0.19, 0.39);
                    return clamp(
                        center + range * cos(6.2831853 * (phase + offset)),
                        vec3(0.0),
                        vec3(1.0));
                }

                float sampled_alpha(vec2 uv, float radius) {
                    float diagonal = radius * 0.70710678;
                    float alpha = 0.0;
                    alpha = max(alpha, sample_source(uv + vec2(radius, 0.0)).a);
                    alpha = max(alpha, sample_source(uv + vec2(-radius, 0.0)).a);
                    alpha = max(alpha, sample_source(uv + vec2(0.0, radius)).a);
                    alpha = max(alpha, sample_source(uv + vec2(0.0, -radius)).a);
                    alpha = max(alpha, sample_source(uv + vec2(diagonal, diagonal)).a);
                    alpha = max(alpha, sample_source(uv + vec2(-diagonal, diagonal)).a);
                    alpha = max(alpha, sample_source(uv + vec2(diagonal, -diagonal)).a);
                    alpha = max(alpha, sample_source(uv + vec2(-diagonal, -diagonal)).a);
                    return alpha;
                }

                void fragment() {
                    vec4 source = sample_source(UV);
                    vec4 modulation = COLOR;
                    float flow =
                        (UV.x * 0.78 + UV.y * 0.36) * rainbow_frequency +
                        TIME * rainbow_speed;
                    vec3 color = spectral_palette(fract(flow));
                    float shimmer = 0.96 + 0.08 * sin(
                        6.2831853 * (flow * 1.7 - TIME * rainbow_speed * 0.35));

                    float outer_alpha = max(
                        0.0,
                        sampled_alpha(UV, glow_radius_ratio) - source.a);
                    outer_alpha = max(
                        outer_alpha,
                        (sampled_alpha(UV, glow_radius_ratio * 0.52) - source.a) * 1.22);
                    float glow_alpha = clamp(
                        outer_alpha * glow_strength,
                        0.0,
                        1.0);
                    float final_alpha = source.a + glow_alpha * (1.0 - source.a);
                    vec3 flowing_color = color * shimmer;
                    vec3 glow_color = spectral_palette(fract(flow + 0.08));
                    vec3 premultiplied =
                        flowing_color * source.a +
                        glow_color * glow_alpha * (1.0 - source.a);
                    vec3 final_color = final_alpha > 0.0001
                        ? premultiplied / final_alpha
                        : vec3(0.0);

                    // Source RGB is deliberately ignored: note art is a
                    // silhouette mask. Black Curse artwork therefore receives
                    // the same full flowing color as every other Note shape.
                    COLOR = vec4(
                        final_color * modulation.rgb,
                        final_alpha * modulation.a);
                }
                """
        };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("note_texture", noteTexture);
        material.SetShaderParameter(
            "rainbow_speed",
            MgrVisualTuning.Notes.OmniaNoteRainbowSpeed);
        material.SetShaderParameter(
            "rainbow_frequency",
            MgrVisualTuning.Notes.OmniaNoteRainbowFrequency);
        material.SetShaderParameter(
            "glow_radius_ratio",
            MgrVisualTuning.Notes.ArtworkGlowRadiusRatio);
        material.SetShaderParameter(
            "glow_strength",
            MgrVisualTuning.Notes.ArtworkGlowStrength);
        material.SetShaderParameter(
            "canvas_margin_ratio",
            MgrVisualTuning.Notes.ArtworkGlowCanvasMarginRatio);
        return material;
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());

    private sealed class OmniaSpark
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Size;
        public float Rotation;
        public float Spin;
        public Color Color;
    }
}
