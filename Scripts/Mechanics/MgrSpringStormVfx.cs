using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MGRMod.Powers;

namespace MGRMod.Mechanics;

/// <summary>
/// Brief full-screen Spring Storm flash. The old implementation held the image
/// and shake for the entire turn; the card now uses one readable accent and
/// immediately returns the battlefield to normal.
/// </summary>
public static class MgrSpringStormVfx
{
    private static MgrSpringStormOverlay? _active;

    public static void Show(Player player)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(player) ||
            NCombatRoom.Instance is null)
        {
            return;
        }

        if (_active is not null && GodotObject.IsInstanceValid(_active))
        {
            _active.Restart(player);
            return;
        }

        var overlay = new MgrSpringStormOverlay();
        overlay.Initialize(player);
        _active = overlay;
        if (NGame.Instance?.CurrentRunNode?.GlobalUi is { } globalUi)
        {
            globalUi.AddChildSafely(overlay);
        }
        else
        {
            _active = null;
            overlay.QueueFree();
        }
    }

    public static void Hide(Player player)
    {
        if (_active is null || !GodotObject.IsInstanceValid(_active))
        {
            _active = null;
            return;
        }

        if (ReferenceEquals(_active.Player, player))
            _active.BeginFadeOut();
    }

    internal static void NotifyFreed(MgrSpringStormOverlay overlay)
    {
        if (ReferenceEquals(_active, overlay))
            _active = null;
    }
}

internal sealed partial class MgrSpringStormOverlay : Control
{
    private const string TexturePath =
        $"{Entry.ResPath}/images/vfx/SpringStormVfx.png";

    private Texture2D? _texture;
    private Vector2 _shakeOffset;
    private Vector2 _shakeTarget;
    private float _shakeTimer;
    private float _visibleElapsed;
    private float _fadeElapsed;
    private bool _fading;

    public Player? Player { get; private set; }

    public void Initialize(Player player) => Player = player;

    public void Restart(Player player)
    {
        Player = player;
        _fading = false;
        _fadeElapsed = 0f;
        _visibleElapsed = 0f;
        Visible = true;
        QueueRedraw();
    }

    public void BeginFadeOut()
    {
        if (_fading)
            return;

        _fading = true;
        _fadeElapsed = 0f;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = MgrVisualTuning.SpringStormVfx.ZIndex;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        _texture = ResourceLoader.Load<Texture2D>(TexturePath);
        if (_texture is null)
        {
            Entry.Logger.Warn($"Missing Spring Storm VFX texture: {TexturePath}");
            QueueFree();
            return;
        }

        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float seconds = (float)delta;
        _visibleElapsed += seconds;
        if (!_fading &&
            (_visibleElapsed >= MgrVisualTuning.SpringStormVfx.FlashHoldSeconds ||
             NCombatRoom.Instance is null))
            BeginFadeOut();

        _shakeTimer -= seconds;
        if (_shakeTimer <= 0f)
        {
            _shakeTimer = MgrVisualTuning.SpringStormVfx.ShakeTargetSeconds;
            float amplitude = MgrVisualTuning.SpringStormVfx.ShakeAmplitude;
            _shakeTarget = new Vector2(
                Random.Shared.NextSingle() * amplitude * 2f - amplitude,
                Random.Shared.NextSingle() * amplitude * 2f - amplitude);
        }

        float smoothing = 1f - MathF.Exp(
            -MgrVisualTuning.SpringStormVfx.ShakeSmoothing * seconds);
        _shakeOffset = _shakeOffset.Lerp(_shakeTarget, smoothing);

        if (_fading)
        {
            _fadeElapsed += seconds;
            if (_fadeElapsed >= MgrVisualTuning.SpringStormVfx.FadeOutSeconds)
            {
                QueueFree();
                return;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_texture is null)
            return;

        float fade = !_fading
            ? 1f
            : 1f - Math.Clamp(
                _fadeElapsed / MgrVisualTuning.SpringStormVfx.FadeOutSeconds,
                0f,
                1f);
        float padding = MgrVisualTuning.SpringStormVfx.DrawPadding;
        Rect2 drawRect = new(
            new Vector2(-padding, -padding) + _shakeOffset,
            Size + Vector2.One * padding * 2f);
        DrawTextureRect(
            _texture,
            drawRect,
            tile: false,
            modulate: Colors.White with
            {
                A = MgrVisualTuning.SpringStormVfx.Opacity * fade
            });
    }

    public override void _ExitTree()
    {
        MgrSpringStormVfx.NotifyFreed(this);
    }
}
