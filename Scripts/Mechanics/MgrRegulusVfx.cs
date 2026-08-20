using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Regulus's one-per-play aurora prelude. Several rainbow ribbons travel along
/// curved and spiral paths into the performer before the fourteen-hit volley
/// begins. The effect is presentation-only and never participates in targeting
/// or damage state.
/// </summary>
public static class MgrRegulusVfx
{
    public static async Task PlayAuroraConvergence(CardModel sourceCard)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(sourceCard.Owner) ||
            NCombatRoom.Instance is not { } room ||
            room.GetCreatureNode(sourceCard.Owner.Creature) is not { } creatureNode)
        {
            return;
        }

        var visual = new MgrRegulusAuroraVisual
        {
            GlobalPosition = creatureNode.VfxSpawnPosition
        };
        room.CombatVfxContainer.AddChildSafely(visual);

        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            MgrVisualTuning.RegulusVfx.ConvergenceSeconds));
    }
}

internal sealed partial class MgrRegulusAuroraVisual : Node2D
{
    private readonly AuroraStrand[] _strands;
    private float _age;

    public MgrRegulusAuroraVisual()
    {
        int count = MgrVisualTuning.RegulusVfx.StrandCount;
        _strands = new AuroraStrand[count];
        for (int index = 0; index < count; index++)
            _strands[index] = AuroraStrand.Create(index, count);
    }

    public override void _Ready()
    {
        ZIndex = MgrVisualTuning.RegulusVfx.ZIndex;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= MgrVisualTuning.RegulusVfx.LifetimeSeconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float convergence = MgrVisualTuning.RegulusVfx.ConvergenceSeconds;
        float progress = Math.Clamp(_age / convergence, 0f, 1f);
        float entry = Mathf.SmoothStep(0f, 1f, Math.Clamp(progress / 0.16f, 0f, 1f));
        float fade = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Math.Clamp(
                (_age - convergence) /
                Math.Max(0.001f, MgrVisualTuning.RegulusVfx.FadeSeconds),
                0f,
                1f));

        for (int index = 0; index < _strands.Length; index++)
            DrawStrand(_strands[index], progress, entry * fade, index);

        DrawConvergenceHalo(progress, fade);
    }

    private void DrawStrand(
        AuroraStrand strand,
        float globalProgress,
        float envelope,
        int strandIndex)
    {
        float delayedProgress = Math.Clamp(
            (globalProgress - strand.Delay) / (1f - strand.Delay),
            0f,
            1f);
        float head = EaseOutCubic(delayedProgress);
        float tail = Math.Max(
            0f,
            head - MgrVisualTuning.RegulusVfx.TrailProgressLength);
        if (head <= 0.001f)
            return;

        int segments = MgrVisualTuning.RegulusVfx.SegmentsPerStrand;
        Vector2 previous = strand.Sample(tail);
        for (int segment = 1; segment <= segments; segment++)
        {
            float along = segment / (float)segments;
            float pathT = Mathf.Lerp(tail, head, along);
            Vector2 current = strand.Sample(pathT);
            float ribbonEnvelope = MathF.Sin(along * MathF.PI);
            float arrivalGlow = Mathf.SmoothStep(0f, 1f, head) *
                Mathf.SmoothStep(0f, 1f, along);
            float alpha = envelope *
                (0.30f + ribbonEnvelope * 0.70f) *
                (0.72f + arrivalGlow * 0.28f);
            float hue = Repeat01(
                strand.HueOffset +
                pathT * 0.34f +
                _age * MgrVisualTuning.RegulusVfx.RainbowShiftPerSecond);
            Color color = Color.FromHsv(hue, 0.62f, 1f);

            DrawLine(
                previous,
                current,
                color with
                {
                    A = alpha * MgrVisualTuning.RegulusVfx.GlowAlpha
                },
                MgrVisualTuning.RegulusVfx.GlowWidth,
                true);
            DrawLine(
                previous,
                current,
                color.Lerp(Colors.White, 0.24f) with
                {
                    A = alpha * MgrVisualTuning.RegulusVfx.CoreAlpha
                },
                MgrVisualTuning.RegulusVfx.CoreWidth +
                (strandIndex % 3) * 0.18f,
                true);

            previous = current;
        }

        Vector2 headPosition = strand.Sample(head);
        float headRadius = 3.4f + 2.2f * MathF.Sin(
            Math.Clamp(delayedProgress, 0f, 1f) * MathF.PI);
        Color headColor = Color.FromHsv(
            Repeat01(strand.HueOffset + _age * 0.23f),
            0.46f,
            1f) with
        {
            A = envelope * 0.78f
        };
        DrawCircle(headPosition, headRadius, headColor);
    }

    private void DrawConvergenceHalo(float progress, float fade)
    {
        float gather = Mathf.SmoothStep(
            0f,
            1f,
            Math.Clamp((progress - 0.56f) / 0.44f, 0f, 1f));
        if (gather <= 0f)
            return;

        float pulse = MathF.Sin(gather * MathF.PI);
        float radius = Mathf.Lerp(78f, 27f, gather);
        float rotation = _age * 2.1f;
        DrawCircle(
            Vector2.Zero,
            24f + pulse * 25f,
            new Color(0.82f, 0.78f, 1f, 0.10f * gather * fade));

        for (int index = 0; index < 3; index++)
        {
            float hue = Repeat01(index / 3f + _age * 0.18f);
            DrawArc(
                Vector2.Zero,
                radius + index * 8f,
                rotation + index * 1.47f,
                rotation + index * 1.47f + MathF.PI * 1.38f,
                42,
                Color.FromHsv(hue, 0.55f, 1f) with
                {
                    A = (0.18f + gather * 0.46f) * fade
                },
                1.7f + index * 0.35f,
                true);
        }

        for (int index = 0; index < 10; index++)
        {
            float angle = rotation * 1.35f + index * Mathf.Tau / 10f;
            Vector2 center = Vector2.FromAngle(angle) *
                (radius * 0.74f + index % 2 * 8f);
            float size = 2.4f + index % 3 * 0.8f;
            Color sparkle = Color.FromHsv(
                Repeat01(index / 10f + _age * 0.26f),
                0.48f,
                1f) with
            {
                A = gather * fade * 0.82f
            };
            DrawLine(
                center - Vector2.Right * size,
                center + Vector2.Right * size,
                sparkle,
                1.3f,
                true);
            DrawLine(
                center - Vector2.Up * size,
                center + Vector2.Up * size,
                sparkle,
                1.3f,
                true);
        }
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float Repeat01(float value) => value - MathF.Floor(value);

    private sealed record AuroraStrand(
        float StartAngle,
        float RadiusX,
        float RadiusY,
        float Turns,
        float Bend,
        float WaveAmplitude,
        float WaveCount,
        float WavePhase,
        float Delay,
        float HueOffset)
    {
        public static AuroraStrand Create(int index, int count)
        {
            float distributedAngle = index * Mathf.Tau / count;
            return new AuroraStrand(
                StartAngle: distributedAngle + RandomRange(-0.34f, 0.34f),
                RadiusX: RandomRange(330f, 610f),
                RadiusY: RandomRange(210f, 390f),
                Turns: RandomRange(0.34f, 0.86f) *
                    (index % 2 == 0 ? 1f : -1f),
                Bend: RandomRange(-64f, 64f),
                WaveAmplitude: RandomRange(9f, 24f),
                WaveCount: RandomRange(1.15f, 2.35f),
                WavePhase: RandomRange(-MathF.PI, MathF.PI),
                Delay: RandomRange(0f, 0.12f),
                HueOffset: Repeat01(index / (float)count + RandomRange(-0.06f, 0.06f)));
        }

        public Vector2 Sample(float progress)
        {
            float t = Math.Clamp(progress, 0f, 1f);
            float remaining = MathF.Pow(1f - t, 0.88f);
            float angle = StartAngle + Turns * Mathf.Tau * t;
            Vector2 spiral = new(
                MathF.Cos(angle) * RadiusX * remaining,
                MathF.Sin(angle) * RadiusY * remaining);
            Vector2 direction = spiral.LengthSquared() > 0.001f
                ? spiral.Normalized()
                : Vector2.Right;
            Vector2 normal = new(-direction.Y, direction.X);
            float endpointEnvelope = MathF.Sin(MathF.PI * t);
            float arc = Bend * endpointEnvelope;
            float wave = MathF.Sin(
                t * MathF.PI * WaveCount + WavePhase) *
                WaveAmplitude * endpointEnvelope;
            return spiral + normal * (arc + wave);
        }

        private static float RandomRange(float minimum, float maximum) =>
            Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
    }
}
