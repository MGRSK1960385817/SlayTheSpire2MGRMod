using Godot;

namespace MGRMod.Mechanics;

/// <summary>
/// Presentation-only empty-slot frame. The eight dashed segments rotate as one
/// object while a fixed-color highlight and a small luminous mote travel around
/// the circumference as one linked trail. The mote stays just ahead of the
/// highlighted dash, visually pulling it around the slot.
/// </summary>
public partial class MgrRotatingNoteSlotFrame : Node2D
{
    private float _flowElapsed;
    private float _breathElapsed;
    private float _baseRotation;
    private float _rotationSpeed;
    private float _trailPhase;
    private float _trailAngularSpeed;
    private float _breathPhase;
    private bool _isPerforming;

    public void Initialize(int slotIndex)
    {
        _flowElapsed = 0f;
        _breathElapsed = 0f;
        _baseRotation = Random.Shared.NextSingle() * MathF.Tau;
        _trailPhase =
            slotIndex * MathF.Tau / Math.Max(1, MgrVisualTuning.Notes.EmptySlotDashCount) +
            Random.Shared.NextSingle() * MathF.Tau;
        _breathPhase = Random.Shared.NextSingle() * MathF.Tau;

        float rotationMultiplier = RandomRange(
            MgrVisualTuning.Notes.EmptySlotRotationMultiplierMin,
            MgrVisualTuning.Notes.EmptySlotRotationMultiplierMax);
        float rotationDirection = Random.Shared.NextSingle() < 0.24f ? -1f : 1f;
        _rotationSpeed = DegreesToRadians(
            MgrVisualTuning.Notes.EmptySlotRotationDegreesPerSecond *
            rotationMultiplier * rotationDirection);
        float trailDirection = Random.Shared.NextSingle() < 0.35f ? -1f : 1f;
        _trailAngularSpeed = RandomRange(
            MgrVisualTuning.Notes.EmptySlotHighlightAngularSpeedMin,
            MgrVisualTuning.Notes.EmptySlotHighlightAngularSpeedMax) *
            trailDirection;

        Rotation = _baseRotation;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float elapsedDelta = (float)delta;
        float flowMultiplier = _isPerforming
            ? MathF.Max(
                1f,
                MgrVisualTuning.Performances.StaffPerformingFlowSpeedMultiplier)
            : 1f;
        float flowDelta = elapsedDelta * flowMultiplier;
        _flowElapsed += flowDelta;
        _breathElapsed += elapsedDelta;
        _baseRotation = MathF.IEEERemainder(
            _baseRotation + _rotationSpeed * flowDelta,
            MathF.Tau);
        Rotation = _baseRotation;
        float breath = 1f + MathF.Sin(
            _breathElapsed * MgrVisualTuning.Notes.EmptySlotBreathSpeed +
            _breathPhase) * MgrVisualTuning.Notes.EmptySlotBreathAmplitude;
        Scale = Vector2.One * breath;

        int dashCount = Math.Max(1, GetChildCount());
        float trailAngle = GetTrailAngle();
        Color accent = MgrVisualTuning.Performances.PerformanceAccentColor;
        Color baseColor = MgrVisualTuning.Notes.EmptySlotBaseColor.Lerp(accent, 0.18f);
        for (int index = 0; index < dashCount; index++)
        {
            if (GetChild(index) is not Line2D dash)
                continue;

            float dashAngle = MathF.Tau * index / dashCount;
            float wave = 0.5f + 0.5f * MathF.Cos(
                dashAngle - trailAngle);
            float highlight = MathF.Pow(wave, 5f);
            Color color = baseColor.Lerp(accent, highlight);
            color.A = Mathf.Lerp(
                MgrVisualTuning.Notes.EmptySlotBaseAlpha,
                MgrVisualTuning.Notes.EmptySlotHighlightAlpha,
                highlight);
            dash.DefaultColor = color;
            dash.Width = MgrVisualTuning.Notes.EmptySlotDashWidth +
                highlight * MgrVisualTuning.Notes.EmptySlotHighlightWidthBoost;
        }

        QueueRedraw();
    }

    public void SetPerforming(bool isPerforming) =>
        _isPerforming = isPerforming;

    public override void _Draw()
    {
        // The star uses the exact same phase, speed and direction as the dash
        // highlight. A small signed lead keeps it at the front of the trail.
        float lead = DegreesToRadians(
            MgrVisualTuning.Notes.EmptySlotGlowLeadDegrees) *
            MathF.Sign(_trailAngularSpeed);
        float angle = GetTrailAngle() + lead;
        Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
        Vector2 center = direction * MgrVisualTuning.Notes.EmptySlotGlowOrbitRadius;
        Color accent = MgrVisualTuning.Performances.PerformanceAccentColor;

        Color outerGlow = accent;
        outerGlow.A = 0.055f;
        DrawCircle(
            center,
            MgrVisualTuning.Notes.EmptySlotGlowHaloRadius,
            outerGlow);
        Color innerGlow = accent;
        innerGlow.A = 0.16f;
        DrawCircle(
            center,
            MgrVisualTuning.Notes.EmptySlotGlowHaloRadius * 0.56f,
            innerGlow);
        Color core = accent;
        core.A = 0.96f;
        DrawCircle(
            center,
            MgrVisualTuning.Notes.EmptySlotGlowCoreRadius,
            core);

        float starLength = MgrVisualTuning.Notes.EmptySlotGlowStarLength;
        DrawLine(
            center - Vector2.Right * starLength,
            center + Vector2.Right * starLength,
            core,
            1.25f,
            true);
        DrawLine(
            center - Vector2.Down * starLength,
            center + Vector2.Down * starLength,
            core,
            1.25f,
            true);
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());

    private float GetTrailAngle() =>
        _trailPhase + _flowElapsed * _trailAngularSpeed;

    private static float DegreesToRadians(float degrees) =>
        degrees * (MathF.PI / 180f);
}
