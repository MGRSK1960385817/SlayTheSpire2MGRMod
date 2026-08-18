using Godot;

namespace MGRMod.Mechanics;

internal enum MgrNoteBurstStyle
{
    Entrance,
    Chord,
    RepeatedChord,
    SlotTransition
}

/// <summary>
/// Code-drawn glow and star spray shared by note generation, chord resolution,
/// and empty-slot transitions. It lives behind the note artwork and never owns
/// combat timing or state.
/// </summary>
internal sealed partial class MgrNoteBurstVisual : Node2D
{
    private readonly float[] _angleOffsets = new float[
        Math.Max(
            Math.Max(
                MgrVisualTuning.Notes.ChordBurstParticleCount,
                MgrVisualTuning.Notes.RepeatedChordBurstParticleCount),
            Math.Max(
                MgrVisualTuning.Notes.EntranceBurstParticleCount,
                MgrVisualTuning.Notes.SlotTransitionBurstParticleCount))];
    private readonly float[] _distanceScales = new float[
        Math.Max(
            Math.Max(
                MgrVisualTuning.Notes.ChordBurstParticleCount,
                MgrVisualTuning.Notes.RepeatedChordBurstParticleCount),
            Math.Max(
                MgrVisualTuning.Notes.EntranceBurstParticleCount,
                MgrVisualTuning.Notes.SlotTransitionBurstParticleCount))];
    private readonly float[] _sizeScales = new float[
        Math.Max(
            Math.Max(
                MgrVisualTuning.Notes.ChordBurstParticleCount,
                MgrVisualTuning.Notes.RepeatedChordBurstParticleCount),
            Math.Max(
                MgrVisualTuning.Notes.EntranceBurstParticleCount,
                MgrVisualTuning.Notes.SlotTransitionBurstParticleCount))];

    private MgrNoteBurstStyle _style;
    private Color _color = Colors.White;
    private float _elapsed;
    private bool _active;

    public override void _Ready()
    {
        Visible = false;
        SetProcess(false);
        for (int index = 0; index < _angleOffsets.Length; index++)
        {
            _angleOffsets[index] = Random.Shared.NextSingle() * 0.46f - 0.23f;
            _distanceScales[index] = 0.72f + Random.Shared.NextSingle() * 0.46f;
            _sizeScales[index] = 0.66f + Random.Shared.NextSingle() * 0.70f;
        }
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        _elapsed += (float)delta;
        if (_elapsed >= GetDuration())
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

        float duration = MathF.Max(0.001f, GetDuration());
        float progress = Math.Clamp(_elapsed / duration, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float fade = 1f - progress;
        float styleStrength = _style switch
        {
            MgrNoteBurstStyle.RepeatedChord => 1.18f,
            MgrNoteBurstStyle.Chord => 1f,
            _ => 0.68f
        };

        // Concentric translucent disks provide a soft same-color glow behind
        // the artwork. The star spray remains crisp in front of that glow.
        for (int layer = 4; layer >= 1; layer--)
        {
            Color glow = _color;
            glow.A = fade * styleStrength * 0.025f * layer;
            float radius = Mathf.Lerp(18f, 34f, eased) * layer / 2.4f;
            DrawCircle(Vector2.Zero, radius, glow);
        }

        int particleCount = GetParticleCount();
        float endRadius = GetEndRadius();
        for (int index = 0; index < particleCount; index++)
        {
            float angle = MathF.Tau * index / particleCount + _angleOffsets[index];
            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            float radius = Mathf.Lerp(
                MgrVisualTuning.Notes.NoteBurstStartRadius,
                endRadius * _distanceScales[index],
                eased);
            Vector2 center = direction * radius;
            Color starColor = _color.Lerp(Colors.White, 0.18f + index % 3 * 0.10f);
            starColor.A = fade * styleStrength * (0.62f + index % 4 * 0.08f);

            float starSize = MgrVisualTuning.Notes.NoteBurstStarSize *
                _sizeScales[index] * (0.72f + fade * 0.72f);
            DrawLine(
                center - direction * starSize * 1.7f,
                center + direction * starSize * 0.8f,
                starColor,
                1.25f + fade,
                true);
            DrawLine(
                center - new Vector2(direction.Y, -direction.X) * starSize,
                center + new Vector2(direction.Y, -direction.X) * starSize,
                starColor,
                1.15f,
                true);
        }
    }

    public void Burst(Color color, MgrNoteBurstStyle style)
    {
        _color = color;
        _style = style;
        _elapsed = 0f;
        _active = true;
        Visible = true;
        Rotation = Random.Shared.NextSingle() * 0.30f - 0.15f;
        SetProcess(true);
        QueueRedraw();
    }

    private int GetParticleCount() => _style switch
    {
        MgrNoteBurstStyle.Entrance => MgrVisualTuning.Notes.EntranceBurstParticleCount,
        MgrNoteBurstStyle.Chord => MgrVisualTuning.Notes.ChordBurstParticleCount,
        MgrNoteBurstStyle.RepeatedChord =>
            MgrVisualTuning.Notes.RepeatedChordBurstParticleCount,
        _ => MgrVisualTuning.Notes.SlotTransitionBurstParticleCount
    };

    private float GetDuration() => (float)(_style switch
    {
        MgrNoteBurstStyle.Entrance => MgrVisualTuning.Notes.EntranceBurstSeconds,
        MgrNoteBurstStyle.Chord => MgrVisualTuning.Notes.ChordBurstSeconds,
        MgrNoteBurstStyle.RepeatedChord =>
            MgrVisualTuning.Notes.RepeatedChordBurstSeconds,
        _ => MgrVisualTuning.Notes.SlotTransitionBurstSeconds
    });

    private float GetEndRadius() => _style switch
    {
        MgrNoteBurstStyle.Entrance => MgrVisualTuning.Notes.EntranceBurstEndRadius,
        MgrNoteBurstStyle.Chord => MgrVisualTuning.Notes.ChordBurstEndRadius,
        MgrNoteBurstStyle.RepeatedChord =>
            MgrVisualTuning.Notes.RepeatedChordBurstEndRadius,
        _ => MgrVisualTuning.Notes.SlotTransitionBurstEndRadius
    };
}
