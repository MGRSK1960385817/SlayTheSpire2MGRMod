using Godot;
using MGRMod.Settings;

namespace MGRMod.Characters;

/// <summary>
/// Gives the three-layer procedural ground shadow a gentle, independently
/// tunable breathing motion. Timing is expressed in animation frames so it is
/// easy to coordinate with the texture-sequence character animation.
/// </summary>
public sealed partial class MgrGroundShadow : Node2D
{
    [Export(PropertyHint.Range, "1,120,1")]
    // Forty-eight 24-FPS reference frames make the shadow breathe at half the
    // speed of the original 24-frame cycle: one full breath every two seconds.
    public float BreathCycleFrames { get; set; } = 48f;

    [Export(PropertyHint.Range, "1,60,1")]
    public float ReferenceFramesPerSecond { get; set; } = 24f;

    [Export]
    public Vector2 OuterScaleAmplitude { get; set; } = new(0.045f, 0.12f);

    [Export]
    public Vector2 MiddleScaleAmplitude { get; set; } = new(0.035f, 0.10f);

    [Export]
    public Vector2 CoreScaleAmplitude { get; set; } = new(0.025f, 0.08f);

    [Export(PropertyHint.Range, "-3.14159,3.14159,0.01")]
    public float MiddlePhaseOffset { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "-3.14159,3.14159,0.01")]
    public float CorePhaseOffset { get; set; } = 0.24f;

    private Polygon2D _outer = null!;
    private Polygon2D _middle = null!;
    private Polygon2D _core = null!;
    private Vector2 _outerBaseScale;
    private Vector2 _middleBaseScale;
    private Vector2 _coreBaseScale;
    private double _elapsedSeconds;

    public override void _Ready()
    {
        if (!MgrVisualSettings.ShouldLoadCharacterEffects)
        {
            Visible = false;
            SetProcess(false);
            return;
        }

        _outer = GetNode<Polygon2D>("Outer");
        _middle = GetNode<Polygon2D>("Middle");
        _core = GetNode<Polygon2D>("Core");

        _outerBaseScale = _outer.Scale;
        _middleBaseScale = _middle.Scale;
        _coreBaseScale = _core.Scale;
    }

    public override void _Process(double delta)
    {
        _elapsedSeconds += delta;

        float framesPerSecond = Mathf.Max(1f, ReferenceFramesPerSecond);
        float cycleSeconds = Mathf.Max(1f, BreathCycleFrames) / framesPerSecond;
        float phase = (float)(_elapsedSeconds / cycleSeconds) * Mathf.Tau;

        ApplyBreathingScale(_outer, _outerBaseScale, OuterScaleAmplitude, phase);
        ApplyBreathingScale(
            _middle,
            _middleBaseScale,
            MiddleScaleAmplitude,
            phase + MiddlePhaseOffset);
        ApplyBreathingScale(
            _core,
            _coreBaseScale,
            CoreScaleAmplitude,
            phase + CorePhaseOffset);
    }

    private static void ApplyBreathingScale(
        Node2D layer,
        Vector2 baseScale,
        Vector2 amplitude,
        float phase)
    {
        float wave = Mathf.Sin(phase);
        layer.Scale = baseScale * new Vector2(
            1f + amplitude.X * wave,
            1f + amplitude.Y * wave);
    }
}
