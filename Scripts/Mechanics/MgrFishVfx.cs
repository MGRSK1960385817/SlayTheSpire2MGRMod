using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Lightweight Maguro attack afterimage made from the user-provided fish
/// texture. It is presentation-only and never participates in attack timing,
/// target selection, or damage resolution.
/// </summary>
public sealed partial class MgrFishVfx : Node2D
{
    private const string TexturePath =
        $"{Entry.ResPath}/images/vfx/fish.png";

    private readonly List<FishLayer> _layers = [];
    private Vector2 _travelOffset;
    private float _effectScale = 1f;
    private float _arcHeight;
    private double _elapsed;

    private sealed record FishLayer(
        Sprite2D Sprite,
        float Delay,
        float BaseAlpha,
        float VerticalOffset,
        Color Tint);

    public static MgrFishVfx? Create(
        Creature attacker,
        Creature target,
        float scale = 1f)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is null)
            return null;

        NCreature? attackerNode = NCombatRoom.Instance.GetCreatureNode(attacker);
        NCreature? targetNode = NCombatRoom.Instance.GetCreatureNode(target);
        if (attackerNode is null || targetNode is null)
            return null;

        Vector2 source = attackerNode.VfxSpawnPosition;
        Vector2 targetPosition = targetNode.VfxSpawnPosition;
        float direction = targetPosition.X >= source.X ? 1f : -1f;
        return new MgrFishVfx
        {
            GlobalPosition = source,
            _travelOffset = targetPosition - source +
                new Vector2(
                    direction * MgrVisualTuning.FishVfx.TargetOvershoot,
                    0f),
            _effectScale = Math.Max(0.01f, scale),
            _arcHeight = -MgrVisualTuning.FishVfx.ArcHeight
        };
    }

    public override void _Ready()
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(TexturePath);
        if (texture is null || texture.GetWidth() <= 0)
        {
            Entry.Logger.Warn($"Missing MGR fish VFX texture: {TexturePath}");
            this.QueueFreeSafely();
            return;
        }

        ZIndex = MgrVisualTuning.FishVfx.ZIndex;
        AddLayer(
            texture,
            delay: 0f,
            alpha: MgrVisualTuning.FishVfx.MainOpacity,
            verticalOffset: 0f,
            Colors.White);
        AddLayer(
            texture,
            delay: MgrVisualTuning.FishVfx.TrailDelay,
            alpha: 0.34f,
            verticalOffset: 8f,
            new Color("8bdcff"));
        AddLayer(
            texture,
            delay: MgrVisualTuning.FishVfx.TrailDelay * 2f,
            alpha: 0.20f,
            verticalOffset: -7f,
            new Color("c8a4ff"));
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        float duration = MgrVisualTuning.FishVfx.TravelSeconds;
        bool anyActive = false;

        foreach (FishLayer layer in _layers)
        {
            float raw = ((float)_elapsed - layer.Delay) / duration;
            if (raw < 0f)
            {
                layer.Sprite.Visible = false;
                anyActive = true;
                continue;
            }

            float t = Mathf.Clamp(raw, 0f, 1f);
            if (t >= 1f)
            {
                layer.Sprite.Visible = false;
                continue;
            }

            anyActive = true;
            layer.Sprite.Visible = true;
            float eased = 1f - MathF.Pow(1f - t, 3f);
            float arc = MathF.Sin(t * MathF.PI) * _arcHeight;
            layer.Sprite.Position = _travelOffset * eased +
                new Vector2(0f, arc + layer.VerticalOffset);

            float pulse = 0.88f + 0.18f * MathF.Sin(t * MathF.PI);
            float imageScale = MgrVisualTuning.FishVfx.DesiredWidth /
                layer.Sprite.Texture.GetWidth();
            layer.Sprite.Scale = Vector2.One * imageScale * _effectScale * pulse;
            layer.Sprite.Rotation = MathF.Sin(t * MathF.PI * 2f) * 0.035f;

            float fade = t <= MgrVisualTuning.FishVfx.FadeStartFraction
                ? 1f
                : 1f - (t - MgrVisualTuning.FishVfx.FadeStartFraction) /
                    (1f - MgrVisualTuning.FishVfx.FadeStartFraction);
            layer.Sprite.Modulate = layer.Tint with
            {
                A = layer.BaseAlpha * Mathf.Clamp(fade, 0f, 1f)
            };
        }

        if (!anyActive)
            this.QueueFreeSafely();
    }

    private void AddLayer(
        Texture2D texture,
        float delay,
        float alpha,
        float verticalOffset,
        Color tint)
    {
        var sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            FlipH = _travelOffset.X < 0f,
            Modulate = tint with { A = alpha },
            Visible = delay <= 0f
        };
        AddChild(sprite);
        _layers.Add(new FishLayer(
            sprite,
            delay,
            alpha,
            verticalOffset,
            tint));
    }
}
