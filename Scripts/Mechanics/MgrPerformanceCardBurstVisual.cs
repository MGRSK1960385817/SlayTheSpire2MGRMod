using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Code-drawn star spray for a Performance card trigger. It deliberately uses
/// streaks and four-point stars instead of a circular aura, keeping the effect
/// bright and musical without restoring the old purple ring.
/// </summary>
internal sealed partial class MgrPerformanceCardBurstVisual : Node2D
{
    private readonly float[] _angleOffsets = new float[
        MgrVisualTuning.Performances.CardBurstParticleCount];
    private readonly float[] _distanceScales = new float[
        MgrVisualTuning.Performances.CardBurstParticleCount];
    private readonly float[] _sizeScales = new float[
        MgrVisualTuning.Performances.CardBurstParticleCount];

    private float _elapsed;
    private bool _active;

    public override void _Ready()
    {
        ZIndex = 32;
        Visible = false;
        SetProcess(false);
        for (int index = 0; index < _angleOffsets.Length; index++)
        {
            _angleOffsets[index] = Random.Shared.NextSingle() * 0.34f - 0.17f;
            _distanceScales[index] = 0.78f + Random.Shared.NextSingle() * 0.38f;
            _sizeScales[index] = 0.72f + Random.Shared.NextSingle() * 0.62f;
        }
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        _elapsed += (float)delta;
        if (_elapsed >= MgrVisualTuning.Performances.CardBurstSeconds)
        {
            _active = false;
            Visible = false;
            SetProcess(false);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_active)
            return;

        float duration = MathF.Max(
            0.001f,
            (float)MgrVisualTuning.Performances.CardBurstSeconds);
        float progress = Math.Clamp(_elapsed / duration, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float alpha = 1f - progress;
        int count = _angleOffsets.Length;
        for (int index = 0; index < count; index++)
        {
            float angle = MathF.Tau * index / count + _angleOffsets[index];
            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            float radius = Mathf.Lerp(
                MgrVisualTuning.Performances.CardBurstStartRadius,
                MgrVisualTuning.Performances.CardBurstEndRadius *
                    _distanceScales[index],
                eased);
            Vector2 center = direction * radius;
            Color color = GetParticleColor(index);
            color.A = alpha * (0.64f + 0.30f * MathF.Sin(index * 2.17f + eased));

            float length = (9f + 11f * (1f - progress)) * _sizeScales[index];
            DrawLine(
                center - direction * length,
                center + direction * length * 0.28f,
                color,
                1.4f + 1.8f * (1f - progress),
                antialiased: true);

            float starSize = (2.4f + 3.2f * alpha) * _sizeScales[index];
            DrawLine(
                center + new Vector2(-starSize, 0f),
                center + new Vector2(starSize, 0f),
                color,
                1.2f,
                antialiased: true);
            DrawLine(
                center + new Vector2(0f, -starSize),
                center + new Vector2(0f, starSize),
                color,
                1.2f,
                antialiased: true);
        }
    }

    public void Burst()
    {
        _elapsed = 0f;
        _active = true;
        Visible = true;
        SetProcess(true);
        Rotation = Random.Shared.NextSingle() * 0.18f - 0.09f;
        QueueRedraw();
    }

    private static Color GetParticleColor(int index) => (index % 6) switch
    {
        0 => new Color("fff4bd"),
        1 => new Color("ffffff"),
        2 => new Color("aeeeff"),
        3 => new Color("efc3ff"),
        4 => new Color("bfffd5"),
        _ => new Color("ffd0dd")
    };
}
