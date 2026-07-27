using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Presentation-only motion for an empty note-slot border.
///
/// The Runesmith rune frame uses a looping AnimationPlayer and advances each
/// new frame to a random point on that timeline. MGR keeps its lighter dashed
/// outline, but follows the same principles: persistent looping motion, a
/// randomized starting phase, and a small non-linear drift so adjacent slots
/// do not rotate in lockstep.
/// </summary>
public partial class MgrRotatingNoteSlotFrame : Node2D
{
    private float _elapsed;
    private float _baseRotation;
    private float _rotationSpeed;
    private float _wobblePhase;
    private float _wobbleAngularSpeed;

    public void Initialize(int slotIndex)
    {
        _elapsed = 0f;

        // Give neighboring slots a stable offset, then add a visual-only random
        // phase. This is deliberately independent from combat/run RNG.
        _baseRotation =
            slotIndex * MathF.Tau / Math.Max(1, MgrVisualTuning.Notes.EmptySlotDashCount) +
            Random.Shared.NextSingle() * MathF.Tau;
        _wobblePhase = Random.Shared.NextSingle() * MathF.Tau;

        _rotationSpeed = DegreesToRadians(
            MgrVisualTuning.Notes.EmptySlotRotationDegreesPerSecond *
            (1f + SymmetricRandom(
                MgrVisualTuning.Notes.EmptySlotRotationSpeedVariance)));
        _wobbleAngularSpeed =
            MgrVisualTuning.Notes.EmptySlotRotationWobbleAngularSpeed *
            (1f + SymmetricRandom(
                MgrVisualTuning.Notes.EmptySlotRotationWobbleSpeedVariance));

        Rotation = _baseRotation;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float elapsedDelta = (float)delta;
        _elapsed += elapsedDelta;
        _baseRotation = MathF.IEEERemainder(
            _baseRotation + _rotationSpeed * elapsedDelta,
            MathF.Tau);

        float wobble = MathF.Sin(
            _elapsed * _wobbleAngularSpeed + _wobblePhase) *
            DegreesToRadians(
                MgrVisualTuning.Notes.EmptySlotRotationWobbleDegrees);
        Rotation = _baseRotation + wobble;
    }

    private static float SymmetricRandom(float radius) =>
        (Random.Shared.NextSingle() * 2f - 1f) * radius;

    private static float DegreesToRadians(float degrees) =>
        degrees * (MathF.PI / 180f);
}
