using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.TestSupport;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Full-screen filters whose lifetime is owned by an awaited player choice.
/// Disposing the returned lease removes the filter, so cancellation, normal
/// completion and exceptions cannot leave a screen effect behind.
/// </summary>
public static class MgrSelectionScreenVfx
{
    public static IDisposable BeginGrayscale(Player player) =>
        Begin(player, MgrSelectionFilterMode.Grayscale);

    public static IDisposable BeginGlitch(Player player) =>
        Begin(player, MgrSelectionFilterMode.Glitch);

    private static IDisposable Begin(
        Player player,
        MgrSelectionFilterMode mode)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(player) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return EmptyLease.Instance;
        }

        var filter = new MgrSelectionScreenFilter(mode);
        globalUi.AddChildSafely(filter);
        return new FilterLease(filter);
    }

    private sealed class FilterLease(MgrSelectionScreenFilter filter) : IDisposable
    {
        private MgrSelectionScreenFilter? _filter = filter;

        public void Dispose()
        {
            if (_filter is { } current && GodotObject.IsInstanceValid(current))
                current.QueueFree();
            _filter = null;
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}

internal enum MgrSelectionFilterMode
{
    Grayscale,
    Glitch
}

internal sealed partial class MgrSelectionScreenFilter : ColorRect
{
    private static Shader? _grayscaleShader;
    private static Shader? _glitchShader;
    private readonly MgrSelectionFilterMode _mode;

    public MgrSelectionScreenFilter(MgrSelectionFilterMode mode) => _mode = mode;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = 850;
        Color = Colors.White;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        Material = new ShaderMaterial
        {
            Shader = _mode == MgrSelectionFilterMode.Grayscale
                ? GetGrayscaleShader()
                : GetGlitchShader()
        };
    }

    private static Shader GetGrayscaleShader() => _grayscaleShader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;

            void fragment() {
                vec4 source = texture(screen_texture, SCREEN_UV);
                float luminance = dot(source.rgb, vec3(0.2126, 0.7152, 0.0722));
                COLOR = vec4(vec3(luminance), source.a);
            }
            """
    };

    private static Shader GetGlitchShader() => _glitchShader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

            float hash(vec2 p) {
                return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
            }

            void fragment() {
                vec2 uv = SCREEN_UV;
                float frame = floor(TIME * 18.0);
                float row = floor(uv.y * 52.0);
                float row_noise = hash(vec2(row, frame));
                float tear = step(0.76, row_noise) * (row_noise - 0.76) * 0.15;
                uv.x += (hash(vec2(row + 9.0, frame)) - 0.5) * tear;

                vec2 pixel_grid = vec2(280.0, 158.0);
                vec2 pixel_uv = floor(uv * pixel_grid) / pixel_grid;
                float channel_shift = 0.0035 + tear * 0.35;
                float red = texture(screen_texture, pixel_uv + vec2(channel_shift, 0.0)).r;
                float green = texture(screen_texture, pixel_uv).g;
                float blue = texture(screen_texture, pixel_uv - vec2(channel_shift, 0.0)).b;
                vec3 color = vec3(red, green, blue);

                float noise = hash(floor(SCREEN_UV * vec2(520.0, 292.0)) + frame);
                float scanline = 0.88 + 0.12 * sin(SCREEN_UV.y * 900.0 + TIME * 24.0);
                color *= scanline;
                color += (noise - 0.5) * 0.13;

                float blue_screen = step(0.965, hash(vec2(frame, 41.0)));
                color = mix(color, vec3(0.025, 0.13, 0.58), blue_screen * 0.72);
                float white_bar = step(0.985, hash(vec2(row, frame + 71.0)));
                color = mix(color, vec3(0.78, 0.91, 1.0), white_bar * 0.55);
                COLOR = vec4(clamp(color, vec3(0.0), vec3(1.0)), 1.0);
            }
            """
    };
}
