using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Persistent purple fireflies while Byakkoya Girl is physically present in
/// the Performance queue. Queue snapshots, rather than card play hooks, own
/// its lifetime so exchanges, forced insertion and early completion all clean
/// the effect up correctly.
/// </summary>
public static class MgrByakkoyaPerformanceVfx
{
    private static readonly Dictionary<Player, MgrByakkoyaFireflyVisual> Visuals = [];

    public static void Update(Player player, bool active)
    {
        if (TestMode.IsOn)
            return;

        if (active)
        {
            if (Visuals.TryGetValue(player, out MgrByakkoyaFireflyVisual? existing) &&
                GodotObject.IsInstanceValid(existing))
            {
                existing.SetActive(true);
                return;
            }

            if (NCombatRoom.Instance is not { } room)
                return;

            var visual = new MgrByakkoyaFireflyVisual(player);
            Visuals[player] = visual;
            room.CombatVfxContainer.AddChildSafely(visual);
            return;
        }

        if (Visuals.TryGetValue(player, out MgrByakkoyaFireflyVisual? current) &&
            GodotObject.IsInstanceValid(current))
        {
            current.SetActive(false);
        }
    }

    public static void ClearAll()
    {
        foreach (MgrByakkoyaFireflyVisual visual in Visuals.Values)
        {
            if (GodotObject.IsInstanceValid(visual))
                visual.QueueFree();
        }

        Visuals.Clear();
    }

    internal static void NotifyFreed(
        Player player,
        MgrByakkoyaFireflyVisual visual)
    {
        if (Visuals.TryGetValue(player, out MgrByakkoyaFireflyVisual? current) &&
            ReferenceEquals(current, visual))
        {
            Visuals.Remove(player);
        }
    }
}

internal sealed partial class MgrByakkoyaFireflyVisual : Node2D
{
    private readonly Player _player;
    private readonly List<Firefly> _fireflies = [];
    private float _age;
    private float _intensity;
    private bool _active = true;

    private sealed record Firefly(
        bool NearCharacter,
        Vector2 BasePosition,
        float Phase,
        float Speed,
        float Size,
        float PulseSpeed,
        Color Color);

    public MgrByakkoyaFireflyVisual(Player player)
    {
        _player = player;
        ZIndex = 18;
        SetProcess(true);
    }

    public override void _Ready()
    {
        Rect2 viewport = GetViewportRect();
        for (int index = 0; index < 30; index++)
        {
            bool near = index < 21;
            Vector2 position = near
                ? new Vector2(
                    RandomRange(-185f, 185f),
                    RandomRange(-145f, 120f))
                : new Vector2(
                    RandomRange(viewport.Position.X, viewport.End.X),
                    RandomRange(viewport.Position.Y + 80f, viewport.End.Y - 80f));
            _fireflies.Add(new Firefly(
                near,
                position,
                RandomRange(0f, Mathf.Tau),
                RandomRange(0.45f, 1.25f),
                RandomRange(1.5f, 3.8f),
                RandomRange(1.4f, 3.1f),
                (index % 3) switch
                {
                    0 => new Color("d9a4ff"),
                    1 => new Color("9d72ff"),
                    _ => new Color("f3d9ff")
                }));
        }
    }

    public void SetActive(bool active)
    {
        _active = active;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float seconds = (float)delta;
        _age += seconds;
        float target = _active ? 1f : 0f;
        _intensity = Mathf.MoveToward(_intensity, target, seconds * 3.4f);
        if (!_active && _intensity <= 0.001f)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(
            _player.Creature);
        if (creatureNode is null)
            return;

        Vector2 characterCenter = creatureNode.VfxSpawnPosition;
        foreach (Firefly firefly in _fireflies)
        {
            float phase = firefly.Phase + _age * firefly.Speed;
            Vector2 drift = new(
                MathF.Sin(phase * 0.83f) * (firefly.NearCharacter ? 18f : 28f),
                MathF.Cos(phase * 1.17f) * (firefly.NearCharacter ? 13f : 22f));
            Vector2 position = (firefly.NearCharacter
                ? characterCenter + firefly.BasePosition
                : firefly.BasePosition) + drift;
            float pulse = 0.28f + 0.72f *
                MathF.Pow(0.5f + 0.5f * MathF.Sin(
                    firefly.Phase + _age * firefly.PulseSpeed), 2f);
            float alpha = _intensity * pulse *
                (firefly.NearCharacter ? 0.82f : 0.38f);
            Color halo = firefly.Color with { A = alpha * 0.14f };
            Color core = firefly.Color with { A = alpha };
            DrawCircle(position, firefly.Size * 4.2f, halo);
            DrawCircle(position, firefly.Size, core);
            DrawLine(
                position - Vector2.Up * firefly.Size * 2.2f,
                position + Vector2.Up * firefly.Size * 2.2f,
                core,
                1f,
                true);
        }
    }

    public override void _ExitTree()
    {
        MgrByakkoyaPerformanceVfx.NotifyFreed(_player, this);
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
}
