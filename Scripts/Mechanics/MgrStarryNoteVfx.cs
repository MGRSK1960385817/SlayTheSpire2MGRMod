using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Shared presentation for every actually generated Starry Note. Keeping this
/// at the Note channel boundary means cards, relics, Powers and Double Notes
/// all receive exactly the same feedback without card-specific exceptions.
/// </summary>
public static class MgrStarryNoteVfx
{
    private static readonly Color[] Palette =
    [
        new("e8c5ff"),
        new("a9ddff"),
        new("fff1ae"),
        new("ffffff")
    ];

    public static void Spawn(Player player)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(player) ||
            NCombatRoom.Instance is not { } room)
        {
            return;
        }

        Rect2 viewport = room.GetViewportRect();
        int count = Random.Shared.Next(
            MgrVisualTuning.StarryNoteVfx.MinimumStarsPerNote,
            MgrVisualTuning.StarryNoteVfx.MaximumStarsPerNote + 1);
        for (int index = 0; index < count; index++)
        {
            var star = new MgrStarryNoteFallingStarVisual();
            star.Initialize(
                new Vector2(
                    RandomRange(-58f, 58f),
                    RandomRange(350f, 470f)),
                RandomRange(80f, 145f),
                RandomRange(1.20f, 1.62f),
                RandomRange(3.0f, 5.6f),
                RandomRange(0f, 0.14f),
                Palette[Random.Shared.Next(Palette.Length)]);
            room.CombatVfxContainer.AddChildSafely(star);
            star.GlobalPosition = new Vector2(
                RandomRange(
                    viewport.Position.X + viewport.Size.X * 0.16f,
                    viewport.End.X - viewport.Size.X * 0.16f),
                viewport.Position.Y - RandomRange(18f, 82f));
        }
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
}

internal sealed partial class MgrStarryNoteFallingStarVisual : Node2D
{
    private Vector2 _velocity;
    private float _gravity;
    private float _lifetime;
    private float _size;
    private float _delay;
    private Color _color;
    private float _elapsed;
    private float _spinSpeed;

    public void Initialize(
        Vector2 velocity,
        float gravity,
        float lifetime,
        float size,
        float delay,
        Color color)
    {
        _velocity = velocity;
        _gravity = gravity;
        _lifetime = lifetime;
        _size = size;
        _delay = delay;
        _color = color;
        _spinSpeed = (Random.Shared.NextSingle() * 2f - 1f) * 2.4f;
        ZIndex = MgrVisualTuning.StarryNoteVfx.ZIndex;
        Visible = delay <= 0f;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed < _delay)
            return;

        Visible = true;
        float activeAge = _elapsed - _delay;
        if (activeAge >= _lifetime)
        {
            QueueFree();
            return;
        }

        float seconds = (float)delta;
        _velocity.Y += _gravity * seconds;
        Position += _velocity * seconds;
        Rotation += _spinSpeed * seconds;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible)
            return;

        float activeAge = MathF.Max(0f, _elapsed - _delay);
        float progress = Math.Clamp(activeAge / MathF.Max(0.001f, _lifetime), 0f, 1f);
        float entrance = Math.Clamp(progress / 0.12f, 0f, 1f);
        float exit = progress < 0.68f
            ? 1f
            : 1f - (progress - 0.68f) / 0.32f;
        float alpha = entrance * Math.Clamp(exit, 0f, 1f);
        float pulse = 0.90f + 0.13f * MathF.Sin(activeAge * 9f + _size);
        float radius = _size * pulse;

        DrawCircle(Vector2.Zero, radius * 3.2f,
            _color with { A = 0.10f * alpha });
        DrawCircle(Vector2.Zero, radius * 1.65f,
            _color with { A = 0.28f * alpha });
        DrawLine(
            new Vector2(-radius, 0f),
            new Vector2(radius, 0f),
            _color with { A = 0.92f * alpha },
            1.8f,
            antialiased: true);
        DrawLine(
            new Vector2(0f, -radius * 1.45f),
            new Vector2(0f, radius * 1.45f),
            _color with { A = 0.92f * alpha },
            2.1f,
            antialiased: true);
        DrawCircle(
            Vector2.Zero,
            MathF.Max(1.2f, radius * 0.25f),
            Colors.White with { A = 0.82f * alpha });
    }
}
