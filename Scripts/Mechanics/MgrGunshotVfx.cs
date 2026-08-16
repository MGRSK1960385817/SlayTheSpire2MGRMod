using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Texture-free hitscan flash used by gun-themed MGR attacks. The effect is
/// deliberately short so damage resolves without a projectile travel delay.
/// </summary>
public sealed partial class MgrGunshotVfx : Node2D
{
    private Vector2 _targetOffset;
    private Vector2[] _tracerPoints = [];
    private Color _tint;
    private float _effectScale = 1f;
    private Tween? _tween;

    public static MgrGunshotVfx? Create(
        Creature attacker,
        Creature target,
        Color tint,
        float scale)
    {
        if (TestMode.IsOn)
            return null;

        NCreature? attackerNode = NCombatRoom.Instance?.GetCreatureNode(attacker);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (attackerNode is null || targetNode is null)
            return null;

        Vector2 source = attackerNode.VfxSpawnPosition;
        return new MgrGunshotVfx
        {
            GlobalPosition = source,
            _targetOffset = targetNode.VfxSpawnPosition - source,
            _tint = tint,
            _effectScale = Math.Max(0.01f, scale)
        };
    }

    public override void _Ready()
    {
        _tracerPoints = BuildTracerCurve();
        AddTracer(_tint with { A = 0.22f }, 16f * _effectScale);
        AddTracer(_tint with { A = 0.85f }, 5f * _effectScale);
        AddTracer(Colors.White with { A = 0.95f }, 1.8f * _effectScale);
        AddFlash(Vector2.Zero, 24f * _effectScale);
        AddFlash(_targetOffset, 31f * _effectScale);
        TaskHelper.RunSafely(AnimateAndFree());
    }

    public override void _ExitTree() => _tween?.Kill();

    private void AddTracer(Color color, float width)
    {
        AddChild(new Line2D
        {
            Points = _tracerPoints,
            Width = width,
            DefaultColor = color,
            Antialiased = true,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round
        });
    }

    /// <summary>
    /// Builds one deliberately asymmetric cubic arc for this shot. The broad
    /// Bézier bend keeps repeated shots separated, while a smaller two-lobed
    /// normal displacement prevents the trajectory from reading as a tidy,
    /// mirrored parabola. Both end points remain exact, so the muzzle and hit
    /// flashes still line up with their creatures.
    /// </summary>
    private Vector2[] BuildTracerCurve()
    {
        const int segments = 26;
        var points = new Vector2[segments + 1];
        float distance = Math.Max(1f, _targetOffset.Length());
        Vector2 direction = _targetOffset / distance;
        Vector2 normal = new(-direction.Y, direction.X);
        float sign = Random.Shared.Next(2) == 0 ? -1f : 1f;
        float arcHeight = sign * RandomRange(
            Math.Min(52f, distance * 0.10f),
            Math.Min(168f, Math.Max(74f, distance * 0.27f)));
        float secondBend = arcHeight * RandomRange(-0.48f, 0.84f);
        Vector2 controlA = _targetOffset * RandomRange(0.18f, 0.34f) +
            normal * arcHeight;
        Vector2 controlB = _targetOffset * RandomRange(0.62f, 0.82f) +
            normal * secondBend;
        float ripple = Math.Min(42f, distance * RandomRange(0.025f, 0.065f));
        float ripplePhase = RandomRange(-1.1f, 1.1f);

        for (int index = 0; index <= segments; index++)
        {
            float t = index / (float)segments;
            float inverse = 1f - t;
            Vector2 cubic =
                3f * inverse * inverse * t * controlA +
                3f * inverse * t * t * controlB +
                t * t * t * _targetOffset;
            float endpointEnvelope = MathF.Sin(MathF.PI * t);
            float unevenWave = MathF.Sin(
                t * MathF.PI * 2.35f + ripplePhase) *
                endpointEnvelope * ripple;
            points[index] = cubic + normal * unevenWave;
        }

        points[0] = Vector2.Zero;
        points[^1] = _targetOffset;
        return points;
    }

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());

    private void AddFlash(Vector2 position, float radius)
    {
        for (int index = 0; index < 4; index++)
        {
            float angle = index * MathF.PI / 4f;
            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            AddChild(new Line2D
            {
                Position = position,
                Points = [-direction * radius, direction * radius],
                Width = Math.Max(1.5f, 3.2f * _effectScale),
                DefaultColor = index % 2 == 0 ? Colors.White : _tint,
                Antialiased = true,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round
            });
        }
    }

    private async Task AnimateAndFree()
    {
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "modulate:a", 0f, 0.16f)
            .SetDelay(0.035f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        _tween.TweenProperty(this, "scale", Vector2.One * 0.72f, 0.19f)
            .From(Vector2.One)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);

        await TweenHelper.AwaitFinished(_tween, this);
        this.QueueFreeSafely();
    }
}
