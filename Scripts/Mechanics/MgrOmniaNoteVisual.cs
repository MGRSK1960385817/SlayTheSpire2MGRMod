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
    private double _shapeElapsed;
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
        Material = CreateRainbowMaterial();
        SetProcess(true);
        return true;
    }

    public override void _Process(double delta)
    {
        if (_textures.Count <= 1)
            return;

        _shapeElapsed += delta;
        while (_shapeElapsed >= MgrVisualTuning.Notes.OmniaNoteShapeSeconds)
        {
            _shapeElapsed -= MgrVisualTuning.Notes.OmniaNoteShapeSeconds;
            _shapeIndex = (_shapeIndex + 1) % _textures.Count;
            Texture = _textures[_shapeIndex];
            FitCurrentTextureToDisplaySize();
        }
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

    private static ShaderMaterial CreateRainbowMaterial()
    {
        Shader shader = _rainbowShader ??= new Shader
        {
            Code = """
                shader_type canvas_item;

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
                    return texture(TEXTURE, clamp(uv, vec2(0.0), vec2(1.0))) *
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
                    vec3 center = vec3(0.62, 0.58, 0.70);
                    vec3 range = vec3(0.34, 0.31, 0.27);
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
                    float shimmer = 0.88 + 0.12 * sin(
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
}
