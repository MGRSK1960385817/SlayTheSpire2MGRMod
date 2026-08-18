using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace MGRMod.Mechanics;

/// <summary>
/// Presentation-only card silhouette used by Maguro Dash while it clears the
/// Performance rack. It intentionally is not an NCard: creating a second NCard
/// for the currently resolving model can confuse Tower 2's result-pile lookup.
/// </summary>
internal sealed partial class MgrPerformanceFinisherVisual : Node2D
{
    private Texture2D? _portrait;
    private float _trailStrength;

    public void Initialize(Texture2D portrait, Vector2 startPosition)
    {
        _portrait = portrait;
        Position = startPosition;
        Scale = Vector2.One * 0.62f;
        Modulate = new Color(1f, 1f, 1f, 0f);
        Rotation = -0.08f;
        QueueRedraw();
    }

    public async Task PlayEntrance()
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            return;

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(
                this,
                "scale",
                Vector2.One,
                MgrVisualTuning.Performances.FinisherEntranceSeconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(
            this,
            "modulate",
            new Color(1f, 1f, 1f, 0.94f),
            MgrVisualTuning.Performances.FinisherEntranceSeconds);
        await TweenHelper.AwaitFinished(tween, this);
    }

    public async Task Strike(Vector2 targetPosition, int strikeIndex)
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            return;

        double seconds = Math.Max(
            MgrVisualTuning.Performances.FinisherMinimumStepSeconds,
            MgrVisualTuning.Performances.FinisherFirstStepSeconds -
            strikeIndex * MgrVisualTuning.Performances.FinisherStepAccelerationSeconds);
        _trailStrength = 1f;
        QueueRedraw();

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "position", targetPosition, seconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(
                this,
                "rotation",
                strikeIndex % 2 == 0 ? 0.045f : -0.045f,
                seconds)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(
                this,
                "scale",
                Vector2.One * 1.08f,
                seconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Back);
        await TweenHelper.AwaitFinished(tween, this);

        _trailStrength = 0.28f;
        Scale = Vector2.One;
        QueueRedraw();
    }

    public async Task PlayExit()
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            return;

        _trailStrength = 1f;
        QueueRedraw();
        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(
                this,
                "position:x",
                Position.X - MgrVisualTuning.Performances.FinisherExitDistance,
                MgrVisualTuning.Performances.FinisherExitSeconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(
            this,
            "scale",
            Vector2.One * 0.58f,
            MgrVisualTuning.Performances.FinisherExitSeconds);
        tween.TweenProperty(
            this,
            "modulate",
            new Color(1f, 1f, 1f, 0f),
            MgrVisualTuning.Performances.FinisherExitSeconds);
        await TweenHelper.AwaitFinished(tween, this);
    }

    public override void _Draw()
    {
        Vector2 size = MgrVisualTuning.Performances.FinisherCardSize;
        Rect2 cardRect = new(-size * 0.5f, size);

        // The streaks point right because the proxy cuts through the rack from
        // its right edge toward the left, leaving a bright musical trail behind.
        if (_trailStrength > 0f)
        {
            float length = MgrVisualTuning.Performances.FinisherTrailLength *
                _trailStrength;
            Color[] trailColors =
            [
                new Color(1f, 0.94f, 0.58f, 0.72f * _trailStrength),
                new Color(1f, 0.55f, 0.82f, 0.58f * _trailStrength),
                new Color(0.55f, 0.91f, 1f, 0.52f * _trailStrength)
            ];
            for (int index = 0; index < trailColors.Length; index++)
            {
                float y = (index - 1) * 13f;
                DrawLine(
                    new Vector2(size.X * 0.38f, y),
                    new Vector2(size.X * 0.38f + length, y + (index - 1) * 5f),
                    trailColors[index],
                    2.2f + (1 - Math.Abs(index - 1)) * 1.4f,
                    antialiased: true);
            }
        }

        DrawRect(cardRect.Grow(7f), new Color(0.92f, 0.64f, 1f, 0.14f), true);
        DrawRect(cardRect, new Color("171023"), true);

        if (_portrait is not null)
        {
            Rect2 portraitRect = new(
                cardRect.Position + new Vector2(6f, 8f),
                new Vector2(cardRect.Size.X - 12f, cardRect.Size.Y * 0.61f));
            DrawTextureRect(
                _portrait,
                portraitRect,
                tile: false,
                new Color(1f, 0.96f, 1f, 0.96f));
        }

        DrawRect(cardRect, new Color("fff0a8"), false, 2.4f, antialiased: true);
        DrawLine(
            new Vector2(cardRect.Position.X + 8f, cardRect.End.Y - 20f),
            new Vector2(cardRect.End.X - 8f, cardRect.End.Y - 20f),
            new Color("f0a9ff"),
            2f,
            antialiased: true);
        DrawLine(
            new Vector2(cardRect.Position.X + 15f, cardRect.End.Y - 12f),
            new Vector2(cardRect.End.X - 15f, cardRect.End.Y - 12f),
            new Color("9deaff"),
            1.6f,
            antialiased: true);

        DrawFourPointStar(new Vector2(cardRect.End.X - 8f, cardRect.Position.Y + 7f), 6f);
        DrawFourPointStar(new Vector2(cardRect.Position.X + 8f, cardRect.End.Y - 7f), 4f);
    }

    private void DrawFourPointStar(Vector2 center, float radius)
    {
        Color color = new("fff7ca");
        DrawLine(
            center + new Vector2(-radius, 0f),
            center + new Vector2(radius, 0f),
            color,
            1.7f,
            antialiased: true);
        DrawLine(
            center + new Vector2(0f, -radius),
            center + new Vector2(0f, radius),
            color,
            1.7f,
            antialiased: true);
    }
}
