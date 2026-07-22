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
    private float _bobAngularSpeed;
    private float _breathAngularSpeed;
    private float _baseScale = 1f;
    private int _slotIndex;

    public void Initialize(int slotIndex)
    {
        _slotIndex = slotIndex;
        RandomizeMotion();
        SetProcess(true);
    }

    /// <summary>
    /// Samples a new visual personality when a note enters this slot. The
    /// randomness is presentation-only and therefore must not use run RNG.
    /// </summary>
    public void RandomizeMotion()
    {
        _elapsed = 0f;
        _phase =
            _slotIndex * MgrVisualTuning.Notes.PhaseStep +
            SymmetricRandom(MgrVisualTuning.Notes.PhaseVariance);
        _bobAngularSpeed =
            MgrVisualTuning.Notes.BobAngularSpeed *
            (1f + SymmetricRandom(MgrVisualTuning.Notes.BobSpeedVariance));
        _breathAngularSpeed =
            MgrVisualTuning.Notes.BreathAngularSpeed *
            (1f + SymmetricRandom(MgrVisualTuning.Notes.BreathSpeedVariance));
        _baseScale =
            1f + SymmetricRandom(MgrVisualTuning.Notes.InitialScaleVariance);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;

        float bob = MathF.Sin(_elapsed * _bobAngularSpeed + _phase);
        float breath = MathF.Sin(
            _elapsed * _breathAngularSpeed + _phase * 1.37f);

        Position = new Vector2(0f, bob * MgrVisualTuning.Notes.BobAmplitude);
        float scale =
            _baseScale *
            (1f + breath * MgrVisualTuning.Notes.BreathAmplitude);
        Scale = new Vector2(scale, scale);
    }

    private static float SymmetricRandom(float radius) =>
        (Random.Shared.NextSingle() * 2f - 1f) * radius;
}
