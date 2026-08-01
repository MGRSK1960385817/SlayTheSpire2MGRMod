using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Presentation-only opacity drift for Ghost Note artwork. This node modulates
/// only the sprite, leaving the amount label, hover bounds and combat state
/// untouched.
/// </summary>
public partial class MgrGhostNoteVisual : Sprite2D
{
    private float _phase;
    private float _angularSpeed;

    public override void _Ready()
    {
        _phase = Random.Shared.NextSingle() * MathF.Tau;
        float variance = MgrVisualTuning.Notes.GhostOpacitySpeedVariance;
        _angularSpeed =
            MgrVisualTuning.Notes.GhostOpacityAngularSpeed *
            (1f + SymmetricRandom(variance));
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _phase = MathF.IEEERemainder(
            _phase + _angularSpeed * (float)delta,
            MathF.Tau);
        float wave = 0.5f + 0.5f * MathF.Sin(_phase);
        float alpha = Mathf.Lerp(
            MgrVisualTuning.Notes.GhostOpacityMinimum,
            MgrVisualTuning.Notes.GhostOpacityMaximum,
            wave);
        SelfModulate = new Color(1f, 1f, 1f, alpha);
    }

    private static float SymmetricRandom(float radius) =>
        (Random.Shared.NextSingle() * 2f - 1f) * radius;
}
