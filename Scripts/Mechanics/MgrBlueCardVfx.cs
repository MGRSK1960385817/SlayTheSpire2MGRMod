using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Short, texture-free cues for uncommon cards whose mechanical result was
/// previously expressed only by native numbers or pile movement. These visuals
/// never own gameplay state and never add an awaited delay.
/// </summary>
public static class MgrBlueCardVfx
{
    public static void SpawnLonelyUniverse(Creature target) =>
        Spawn(target, MgrBlueCardCueStyle.LonelyUniverse);

    public static void SpawnWarmUp(Creature target, bool completed) =>
        Spawn(
            target,
            completed
                ? MgrBlueCardCueStyle.WarmUpRelease
                : MgrBlueCardCueStyle.WarmUpCharge);

    public static void SpawnUltramarineGuard(Creature target, int performanceTurns) =>
        Spawn(target, MgrBlueCardCueStyle.UltramarineGuard, performanceTurns);

    public static void SpawnMasterfulBranches(
        Creature target,
        bool phraseStart,
        bool phraseEnd)
    {
        if (phraseStart || phraseEnd)
            Spawn(target, MgrBlueCardCueStyle.MasterfulBranches, 0, phraseStart, phraseEnd);
    }

    public static void SpawnProcrastination(Creature target, bool completed) =>
        Spawn(
            target,
            completed
                ? MgrBlueCardCueStyle.ProcrastinationPotion
                : MgrBlueCardCueStyle.ProcrastinationClock);

    public static void SpawnDonutGuard(Creature target, int performanceCards) =>
        Spawn(target, MgrBlueCardCueStyle.DonutGuard, performanceCards);

    public static void SpawnPuppetClownSwap(Creature target) =>
        Spawn(target, MgrBlueCardCueStyle.PuppetClownSwap);

    private static void Spawn(
        Creature target,
        MgrBlueCardCueStyle style,
        int amount = 0,
        bool firstBranch = false,
        bool secondBranch = false)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        var creatureNode = room.GetCreatureNode(target);
        if (creatureNode is null)
            return;

        var visual = new MgrBlueCardCueVisual();
        visual.Initialize(style, amount, firstBranch, secondBranch);
        visual.GlobalPosition = creatureNode.VfxSpawnPosition + new Vector2(0f, -18f);
        room.CombatVfxContainer.AddChildSafely(visual);
    }
}

internal enum MgrBlueCardCueStyle
{
    LonelyUniverse,
    WarmUpCharge,
    WarmUpRelease,
    UltramarineGuard,
    MasterfulBranches,
    ProcrastinationClock,
    ProcrastinationPotion,
    DonutGuard,
    PuppetClownSwap
}

internal sealed partial class MgrBlueCardCueVisual : Node2D
{
    private static readonly Color Gold = new("ffe69a");
    private static readonly Color Violet = new("b88cff");
    private static readonly Color StarBlue = new("7edcff");
    private static readonly Color Ultramarine = new("547cff");
    private static readonly Color Warm = new("ff8b62");
    private static readonly Color Strength = new("ff6a62");
    private static readonly Color Dexterity = new("72e6a5");
    private static readonly Color Curse = new("8d63ba");

    private MgrBlueCardCueStyle _style;
    private int _amount;
    private bool _firstBranch;
    private bool _secondBranch;
    private float _age;
    private float _lifetime;

    public void Initialize(
        MgrBlueCardCueStyle style,
        int amount,
        bool firstBranch,
        bool secondBranch)
    {
        _style = style;
        _amount = Math.Max(0, amount);
        _firstBranch = firstBranch;
        _secondBranch = secondBranch;
        _lifetime = style is
            MgrBlueCardCueStyle.WarmUpRelease or
            MgrBlueCardCueStyle.ProcrastinationPotion or
            MgrBlueCardCueStyle.PuppetClownSwap
                ? MgrVisualTuning.BlueCardVfx.FinaleLifetimeSeconds
                : MgrVisualTuning.BlueCardVfx.StandardLifetimeSeconds;
    }

    public override void _Ready()
    {
        ZIndex = MgrVisualTuning.BlueCardVfx.ZIndex;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = Math.Clamp(_age / _lifetime, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float alpha = Math.Clamp(progress / 0.12f, 0f, 1f) *
            Math.Clamp((1f - progress) / 0.34f, 0f, 1f);

        switch (_style)
        {
            case MgrBlueCardCueStyle.LonelyUniverse:
                DrawLonelyUniverse(progress, eased, alpha);
                break;
            case MgrBlueCardCueStyle.WarmUpCharge:
                DrawWarmUp(progress, eased, alpha, completed: false);
                break;
            case MgrBlueCardCueStyle.WarmUpRelease:
                DrawWarmUp(progress, eased, alpha, completed: true);
                break;
            case MgrBlueCardCueStyle.UltramarineGuard:
                DrawUltramarine(eased, alpha);
                break;
            case MgrBlueCardCueStyle.MasterfulBranches:
                DrawMasterful(progress, eased, alpha);
                break;
            case MgrBlueCardCueStyle.ProcrastinationClock:
                DrawClock(progress, eased, alpha);
                break;
            case MgrBlueCardCueStyle.ProcrastinationPotion:
                DrawPotion(progress, eased, alpha);
                break;
            case MgrBlueCardCueStyle.DonutGuard:
                DrawDonut(eased, alpha);
                break;
            case MgrBlueCardCueStyle.PuppetClownSwap:
                DrawPuppetSwap(progress, alpha);
                break;
        }
    }

    private void DrawLonelyUniverse(float progress, float eased, float alpha)
    {
        float orbitRadius = Mathf.Lerp(24f, 72f, eased);
        DrawCircle(Vector2.Zero, 22f + 10f * eased,
            new Color(0.22f, 0.12f, 0.44f, alpha * 0.14f));
        DrawArc(Vector2.Zero, orbitRadius, -2.7f, 2.2f, 42,
            Violet with { A = alpha * 0.60f }, 2f, true);

        float angle = -2.7f + progress * 4.9f;
        Vector2 satellite = Vector2.FromAngle(angle) * orbitRadius;
        DrawCircle(satellite, 11f, StarBlue with { A = alpha * 0.12f });
        DrawStar(satellite, 6.5f, StarBlue with { A = alpha });
        DrawStar(Vector2.Zero, 9f, Gold with { A = alpha * 0.95f });
    }

    private void DrawWarmUp(float progress, float eased, float alpha, bool completed)
    {
        float radius = completed
            ? Mathf.Lerp(24f, 94f, eased)
            : Mathf.Lerp(68f, 38f, eased);
        Color main = completed ? Gold : Warm;
        DrawCircle(Vector2.Zero, radius * 0.62f,
            main with { A = alpha * (completed ? 0.14f : 0.08f) });

        for (int index = 0; index < 3; index++)
        {
            float offset = index * 0.42f;
            DrawArc(
                Vector2.Zero,
                radius + index * 9f,
                -2.78f + offset + progress * 1.2f,
                -0.36f + offset + progress * 1.2f,
                24,
                main with { A = alpha * (0.78f - index * 0.16f) },
                completed ? 3.2f : 2.2f,
                true);
        }

        if (completed)
        {
            DrawStar(Vector2.Zero, 12f, Colors.White with { A = alpha });
            for (int index = 0; index < 8; index++)
            {
                Vector2 ray = Vector2.FromAngle(index * Mathf.Tau / 8f) * radius;
                DrawLine(ray * 0.58f, ray, Gold with { A = alpha * 0.72f }, 2f, true);
            }
        }
    }

    private void DrawUltramarine(float eased, float alpha)
    {
        int waves = Math.Clamp(_amount, 1, 9);
        float radius = Mathf.Lerp(26f, 78f, eased);
        DrawCircle(Vector2.Zero, radius * 0.72f,
            Ultramarine with { A = alpha * 0.10f });
        for (int index = 0; index < Math.Min(waves, 5); index++)
        {
            float y = 30f - index * 13f;
            float width = radius * (0.72f + index * 0.08f);
            DrawArc(
                new Vector2(0f, y),
                width,
                MathF.PI + 0.26f,
                Mathf.Tau - 0.26f,
                30,
                StarBlue with { A = alpha * (0.86f - index * 0.10f) },
                2.4f,
                true);
        }
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 44,
            Ultramarine with { A = alpha * 0.72f }, 3.2f, true);
    }

    private void DrawMasterful(float progress, float eased, float alpha)
    {
        float radius = Mathf.Lerp(24f, 76f, eased);
        if (_firstBranch)
        {
            DrawArc(Vector2.Zero, radius, MathF.PI * 0.56f, MathF.PI * 1.44f, 28,
                Strength with { A = alpha * 0.90f }, 4f, true);
            Vector2 point = new(-radius * 0.88f, 0f);
            DrawLine(point, point + new Vector2(21f, -12f),
                Strength with { A = alpha }, 3f, true);
            DrawLine(point, point + new Vector2(21f, 12f),
                Strength with { A = alpha }, 3f, true);
        }

        if (_secondBranch)
        {
            DrawArc(Vector2.Zero, radius, -MathF.PI * 0.44f, MathF.PI * 0.44f, 28,
                Dexterity with { A = alpha * 0.90f }, 4f, true);
            Vector2 point = new(radius * 0.88f, 0f);
            DrawLine(point, point + new Vector2(-21f, -12f),
                Dexterity with { A = alpha }, 3f, true);
            DrawLine(point, point + new Vector2(-21f, 12f),
                Dexterity with { A = alpha }, 3f, true);
        }

        DrawCircle(Vector2.Zero, 10f + 5f * MathF.Sin(progress * MathF.PI),
            Gold with { A = alpha * 0.70f });
    }

    private void DrawClock(float progress, float eased, float alpha)
    {
        float radius = Mathf.Lerp(26f, 57f, eased);
        DrawCircle(Vector2.Zero, radius,
            new Color(0.15f, 0.08f, 0.24f, alpha * 0.18f));
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 42,
            Violet with { A = alpha * 0.84f }, 3f, true);
        for (int index = 0; index < 8; index++)
        {
            Vector2 direction = Vector2.FromAngle(index * Mathf.Tau / 8f);
            DrawLine(direction * (radius - 7f), direction * radius,
                Gold with { A = alpha * 0.72f }, 2f, true);
        }

        float handAngle = -MathF.PI * 0.5f + progress * Mathf.Tau * 0.72f;
        DrawLine(Vector2.Zero, Vector2.FromAngle(handAngle) * radius * 0.68f,
            Gold with { A = alpha }, 3f, true);
        DrawLine(Vector2.Zero, new Vector2(-radius * 0.34f, radius * 0.12f),
            StarBlue with { A = alpha * 0.86f }, 2f, true);
        DrawCircle(Vector2.Zero, 5f, Colors.White with { A = alpha });
    }

    private void DrawPotion(float progress, float eased, float alpha)
    {
        float scale = Mathf.Lerp(0.62f, 1.08f, eased);
        Vector2[] bottle =
        [
            new(-12f, -42f), new(12f, -42f), new(12f, -23f),
            new(29f, -4f), new(24f, 38f), new(-24f, 38f),
            new(-29f, -4f), new(-12f, -23f), new(-12f, -42f)
        ];
        for (int index = 0; index < bottle.Length; index++)
            bottle[index] *= scale;

        DrawPolyline(bottle, Gold with { A = alpha * 0.92f }, 3f, true);
        DrawLine(new Vector2(-22f, 12f) * scale, new Vector2(22f, 12f) * scale,
            Violet with { A = alpha * 0.92f }, 3f, true);
        DrawCircle(new Vector2(0f, 24f) * scale, 18f * scale,
            Violet with { A = alpha * 0.22f });

        for (int index = 0; index < 7; index++)
        {
            float angle = index * Mathf.Tau / 7f + progress * 1.6f;
            Vector2 center = Vector2.FromAngle(angle) * Mathf.Lerp(30f, 82f, eased);
            DrawStar(center, 4.5f, (index % 2 == 0 ? StarBlue : Gold) with
            {
                A = alpha * 0.88f
            });
        }
    }

    private void DrawDonut(float eased, float alpha)
    {
        int cards = Math.Clamp(_amount, 0, 12);
        float outer = Mathf.Lerp(28f, 78f, eased);
        float inner = Math.Max(18f, 49f - cards * 3.2f);
        DrawCircle(Vector2.Zero, outer,
            new Color(0.36f, 0.18f, 0.48f, alpha * 0.11f));
        DrawArc(Vector2.Zero, outer, 0f, Mathf.Tau, 48,
            Gold with { A = alpha * 0.82f }, 4.5f, true);
        DrawArc(Vector2.Zero, inner, 0f, Mathf.Tau, 40,
            Violet with { A = alpha * 0.92f }, 5.5f, true);
        DrawArc(Vector2.Zero, (outer + inner) * 0.5f, 0.35f, 5.92f, 38,
            StarBlue with { A = alpha * 0.46f }, 2f, true);
    }

    private void DrawPuppetSwap(float progress, float alpha)
    {
        Vector2 left = new(-86f, 22f);
        Vector2 right = new(86f, -22f);
        Vector2[] upper = BuildQuadratic(left, new Vector2(0f, -92f), right);
        Vector2[] lower = BuildQuadratic(right, new Vector2(0f, 92f), left);
        DrawPolyline(upper, Gold with { A = alpha * 0.70f }, 2.6f, true);
        DrawPolyline(lower, Curse with { A = alpha * 0.82f }, 2.6f, true);

        Vector2 goldCard = SampleQuadratic(left, new Vector2(0f, -92f), right, progress);
        Vector2 curseCard = SampleQuadratic(right, new Vector2(0f, 92f), left, progress);
        DrawCardGlyph(goldCard, Gold with { A = alpha });
        DrawCardGlyph(curseCard, Curse with { A = alpha });

        float pulse = 1f + 0.18f * MathF.Sin(progress * MathF.PI);
        Vector2 top = new(0f, -12f * pulse);
        Vector2 side = new(12f * pulse, 0f);
        Vector2[] diamond = [top, side, -top, -side, top];
        DrawPolyline(diamond, StarBlue with { A = alpha * 0.90f }, 3f, true);
        DrawCircle(new Vector2(-4f, -2f), 2.2f, Colors.White with { A = alpha });
        DrawCircle(new Vector2(4f, -2f), 2.2f, Colors.White with { A = alpha });
    }

    private void DrawCardGlyph(Vector2 center, Color color)
    {
        Rect2 rect = new(center - new Vector2(8f, 11f), new Vector2(16f, 22f));
        DrawRect(rect, color with { A = color.A * 0.16f }, filled: true);
        DrawRect(rect, color, filled: false, width: 2f, antialiased: true);
    }

    private void DrawStar(Vector2 center, float size, Color color)
    {
        DrawLine(center + new Vector2(-size, 0f), center + new Vector2(size, 0f),
            color, 2f, true);
        DrawLine(center + new Vector2(0f, -size * 1.35f), center + new Vector2(0f, size * 1.35f),
            color, 2.2f, true);
    }

    private static Vector2[] BuildQuadratic(Vector2 start, Vector2 control, Vector2 end)
    {
        const int segments = 24;
        var points = new Vector2[segments + 1];
        for (int index = 0; index <= segments; index++)
            points[index] = SampleQuadratic(start, control, end, index / (float)segments);
        return points;
    }

    private static Vector2 SampleQuadratic(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * start +
            2f * inverse * progress * control +
            progress * progress * end;
    }
}
