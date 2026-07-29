using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Four fixed-color corner brackets for an idle Performance card. They use the
/// same accent as the remaining-turn staff wings and never cycle hue.
/// </summary>
internal sealed partial class MgrPerformanceIdleEdgeVisual : Node2D
{
    private Rect2 _cardRect;
    private float _displayScale = 1f;
    private float _strength = 1f;
    private float _targetStrength = 1f;

    public void Initialize(Rect2 cardRect, float displayScale)
    {
        _displayScale = MathF.Max(0.01f, displayScale);
        float unscaledMargin =
            MgrVisualTuning.Performances.IdleEdgeMargin / _displayScale;
        _cardRect = cardRect.Grow(unscaledMargin);
        ZIndex = 18;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _strength = Mathf.Lerp(
            _strength,
            _targetStrength,
            Math.Clamp((float)delta * 9f, 0f, 1f));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_strength <= 0.01f)
            return;

        float baseWidth =
            MgrVisualTuning.Performances.IdleEdgeBaseWidth / _displayScale;
        float glowWidth =
            MgrVisualTuning.Performances.IdleEdgeGlowWidth / _displayScale;

        Color baseColor = MgrVisualTuning.Performances.PerformanceAccentColor;
        baseColor.A = MgrVisualTuning.Performances.IdleEdgeBaseAlpha *
            _strength;
        Color glowColor = baseColor;
        glowColor.A = MgrVisualTuning.Performances.IdleEdgeGlowAlpha *
            _strength;
        DrawCornerBrackets(glowColor, glowWidth);
        DrawCornerBrackets(baseColor, baseWidth);
    }

    public void SetTriggering(bool isTriggering)
    {
        // The dedicated trigger burst owns the foreground while a card plays.
        // Keeping a trace rather than switching off avoids a visual hard cut.
        _targetStrength = isTriggering ? 0.18f : 1f;
    }

    private void DrawCornerBrackets(Color color, float width)
    {
        float left = _cardRect.Position.X;
        float top = _cardRect.Position.Y;
        float right = _cardRect.End.X;
        float bottom = _cardRect.End.Y;
        float horizontal = _cardRect.Size.X * 0.18f;
        float vertical = _cardRect.Size.Y * 0.12f;

        DrawLine(new(left, top), new(left + horizontal, top), color, width, true);
        DrawLine(new(left, top), new(left, top + vertical), color, width, true);
        DrawLine(new(right, top), new(right - horizontal, top), color, width, true);
        DrawLine(new(right, top), new(right, top + vertical), color, width, true);
        DrawLine(new(left, bottom), new(left + horizontal, bottom), color, width, true);
        DrawLine(new(left, bottom), new(left, bottom - vertical), color, width, true);
        DrawLine(new(right, bottom), new(right - horizontal, bottom), color, width, true);
        DrawLine(new(right, bottom), new(right, bottom - vertical), color, width, true);
    }
}
