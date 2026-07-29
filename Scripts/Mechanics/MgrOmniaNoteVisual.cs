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
        }
    }

    private static ShaderMaterial CreateRainbowMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                uniform float rainbow_speed = 0.22;
                uniform float rainbow_frequency = 1.35;

                vec3 rainbow(float hue) {
                    vec3 phase = fract(hue + vec3(0.0, 0.6666667, 0.3333333));
                    return clamp(abs(phase * 6.0 - 3.0) - 1.0, 0.0, 1.0);
                }

                void fragment() {
                    vec4 source = texture(TEXTURE, UV);
                    float flow = (UV.x + UV.y * 0.45) * rainbow_frequency;
                    vec3 color = rainbow(fract(flow + TIME * rainbow_speed));
                    COLOR = vec4(color, source.a) * COLOR;
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
