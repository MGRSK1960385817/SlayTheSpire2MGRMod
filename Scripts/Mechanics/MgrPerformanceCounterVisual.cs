using Godot;

namespace MGRMod.Mechanics;

/// <summary>
/// Displays remaining Performance turns as a floating beat marker above the
/// card. It deliberately avoids a closed badge/ring: the number sits between
/// two staff wings whose line count follows the displayed remaining turns.
/// </summary>
internal sealed partial class MgrPerformanceCounterVisual : Node2D
{
    private readonly Label _label = new()
    {
        Name = "RemainingPerformances",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    private Vector2 _homePosition;
    private int _displayedRemaining;
    private int _targetRemaining;
    private float _triggerElapsed;
    private float _triggerDurationScale = 1f;
    private bool _triggerActive;
    private bool _changedDuringTrigger;
    private bool _awaitingTriggerCommit;

    public void Initialize(int remaining, float visibleCardHeight)
    {
        _displayedRemaining = Math.Max(0, remaining);
        _targetRemaining = _displayedRemaining;
        _homePosition = new Vector2(
            0f,
            -visibleCardHeight * 0.5f -
                MgrVisualTuning.Performances.RemainingCounterTopGap -
                MgrVisualTuning.Performances.RemainingCounterSize.Y * 0.5f);
        Position = _homePosition;

        _label.Text = _displayedRemaining.ToString();
        _label.Position = -MgrVisualTuning.Performances.RemainingCounterSize * 0.5f;
        _label.Size = MgrVisualTuning.Performances.RemainingCounterSize;
        _label.AddThemeFontSizeOverride(
            "font_size",
            MgrVisualTuning.Performances.RemainingCounterFontSize);
        _label.AddThemeColorOverride(
            "font_color",
            MgrVisualTuning.Performances.RemainingCounterColor);
        _label.AddThemeColorOverride(
            "font_outline_color",
            MgrVisualTuning.Performances.RemainingCounterOutlineColor);
        _label.AddThemeConstantOverride(
            "outline_size",
            MgrVisualTuning.Performances.RemainingCounterOutlineSize);
        AddChild(_label);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float elapsedDelta = (float)delta;

        float triggerProgress = 0f;
        if (_triggerActive)
        {
            _triggerElapsed += elapsedDelta;
            float duration = GetTriggerDuration();
            triggerProgress = Math.Clamp(_triggerElapsed / duration, 0f, 1f);
            if (!_changedDuringTrigger &&
                triggerProgress >= MgrVisualTuning.Performances.RemainingCounterChangeFraction)
            {
                _changedDuringTrigger = true;
                SetDisplayedRemaining(_targetRemaining);
            }

            if (triggerProgress >= 1f)
            {
                _triggerActive = false;
                triggerProgress = 0f;
                Scale = Vector2.One;
            }
        }

        Position = _homePosition;

        if (_triggerActive)
        {
            float pulse = MathF.Sin(triggerProgress * MathF.PI);
            Scale = Vector2.One * (1f + pulse * 0.24f);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float triggerFlash = _triggerActive
            ? MathF.Sin(Math.Clamp(
                _triggerElapsed / GetTriggerDuration(),
                0f,
                1f) * MathF.PI)
            : 0f;
        Color color = MgrVisualTuning.Performances.RemainingCounterColor;
        color.A = Mathf.Lerp(color.A, 0.94f, triggerFlash);

        float gap = MgrVisualTuning.Performances.RemainingCounterWingGap;
        float length = MgrVisualTuning.Performances.RemainingCounterWingLength;
        float width = MgrVisualTuning.Performances.RemainingCounterLineWidth +
            triggerFlash * 1.4f;
        int lineCount = Math.Clamp(
            _displayedRemaining,
            0,
            Math.Max(1, MgrVisualTuning.Performances.RemainingCounterWingLineCount));
        if (lineCount == 0)
            return;

        float groupLengthScale = lineCount switch
        {
            1 => MgrVisualTuning.Performances.RemainingCounterSingleWingLengthScale,
            2 => MgrVisualTuning.Performances.RemainingCounterDoubleWingLengthScale,
            _ => 1f
        };
        float spacing = MgrVisualTuning.Performances.RemainingCounterWingSpacing;
        float firstY = -(lineCount - 1) * spacing * 0.5f;
        for (int index = 0; index < lineCount; index++)
        {
            float y = firstY + index * spacing;
            float rowLength = length * groupLengthScale *
                MathF.Max(0.46f, 1f - index * 0.23f);
            float rowWidth = width * MathF.Max(0.72f, 1f - index * 0.10f);
            DrawLine(
                new(-gap - rowLength, y),
                new(-gap, y),
                color,
                rowWidth,
                true);
            DrawLine(
                new(gap, y),
                new(gap + rowLength, y),
                color,
                rowWidth,
                true);
        }
    }

    public void Refresh(int remaining)
    {
        // Gameplay intentionally commits the real remaining count only after
        // AutoPlay succeeds. Keep the trigger preview stable in that interval:
        // card/glow refreshes must not restore the old model value for a frame.
        if (_triggerActive || _awaitingTriggerCommit)
            return;

        SetDisplayedRemaining(remaining);
    }

    public void PlayTrigger(int displayedRemainingAfterTrigger, float durationScale)
    {
        _targetRemaining = Math.Max(0, displayedRemainingAfterTrigger);
        _triggerDurationScale = Math.Clamp(durationScale, 0.1f, 1f);
        _triggerElapsed = 0f;
        _triggerActive = true;
        _changedDuringTrigger = false;
        _awaitingTriggerCommit = true;
    }

    public void CommitTrigger(int remaining)
    {
        _targetRemaining = Math.Max(0, remaining);
        _awaitingTriggerCommit = false;
        SetDisplayedRemaining(_targetRemaining);
    }

    private void SetDisplayedRemaining(int remaining)
    {
        _displayedRemaining = Math.Max(0, remaining);
        _label.Text = _displayedRemaining.ToString();
    }

    private float GetTriggerDuration() => MathF.Max(
        0.001f,
        (float)MgrVisualTuning.Performances.RemainingCounterPulseSeconds *
        _triggerDurationScale);
}
