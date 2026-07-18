using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Persistent idle-motion node for a filled note slot. Entrance animation is
/// applied to its parent, keeping the continuous bob/breath phase independent
/// from one-shot tweens.
/// </summary>
public partial class MgrFloatingNoteVisual : Node2D
{
    private float _elapsed;
    private float _phase;

    public void Initialize(int slotIndex)
    {
        _phase = slotIndex * MgrVisualTuning.Notes.PhaseStep;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;

        float bob = MathF.Sin(
            _elapsed * MgrVisualTuning.Notes.BobAngularSpeed + _phase);
        float breath = MathF.Sin(
            _elapsed * MgrVisualTuning.Notes.BreathAngularSpeed + _phase * 1.37f);

        Position = new Vector2(0f, bob * MgrVisualTuning.Notes.BobAmplitude);
        float scale = 1f + breath * MgrVisualTuning.Notes.BreathAmplitude;
        Scale = new Vector2(scale, scale);
    }
}
