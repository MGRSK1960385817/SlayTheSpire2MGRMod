using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Animated artwork for the Omnia Note. The shape cycles through the five
/// basic Notes and the Starry Note while a shader continuously moves a rainbow
/// across the currently displayed silhouette.
/// </summary>
public partial class MgrOmniaNoteVisual : Sprite2D
{
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
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                uniform float rainbow_speed = 0.22;
                uniform float rainbow_frequency = 1.35;

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

                void fragment() {
                    vec4 source = texture(TEXTURE, UV);
                    vec4 modulation = COLOR;
                    float flow =
                        (UV.x * 0.78 + UV.y * 0.36) * rainbow_frequency +
                        TIME * rainbow_speed;
                    vec3 color = spectral_palette(fract(flow));
                    float shimmer = 0.88 + 0.12 * sin(
                        6.2831853 * (flow * 1.7 - TIME * rainbow_speed * 0.35));

                    // Source RGB is deliberately ignored: note art is a
                    // silhouette mask. Black Curse artwork therefore receives
                    // the same full flowing color as every other Note shape.
                    COLOR = vec4(
                        color * shimmer * modulation.rgb,
                        source.a * modulation.a);
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
        return material;
    }
}
