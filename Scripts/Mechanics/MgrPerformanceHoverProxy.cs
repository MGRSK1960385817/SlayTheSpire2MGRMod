using Godot;

namespace MGRMod.Mechanics;

/// <summary>
/// Screen-space hitbox that mirrors a card living under the combat creature
/// canvas. The proxy belongs to the native combat UI's free-position container,
/// so it converts all four real card corners into its parent's local space
/// instead of assuming that either canvas has a viewport-origin transform.
/// </summary>
public partial class MgrPerformanceHoverProxy : Control
{
    private Transform2D _lastTargetToViewport;
    private Transform2D _lastProxyParentToViewport;
    private Rect2 _lastTargetRect;
    private bool _hasCachedGeometry;

    public Control? Target { get; set; }
    public Rect2 TargetRect { get; set; }

    public override void _Process(double delta)
    {
        SyncToTarget();
    }

    public void SyncToTarget()
    {
        if (Target is null ||
            !GodotObject.IsInstanceValid(Target) ||
            !Target.IsInsideTree() ||
            !IsInsideTree())
        {
            SetVisibleIfChanged(false);
            _hasCachedGeometry = false;
            return;
        }

        bool targetVisible = Target.IsVisibleInTree();
        SetVisibleIfChanged(targetVisible);
        if (!targetVisible)
        {
            _hasCachedGeometry = false;
            return;
        }

        if (GetParent() is not CanvasItem proxyParent)
        {
            SetVisibleIfChanged(false);
            _hasCachedGeometry = false;
            return;
        }

        Transform2D targetToViewport = Target.GetGlobalTransformWithCanvas();
        Transform2D proxyParentToViewport =
            proxyParent.GetGlobalTransformWithCanvas();
        Rect2 targetRect = TargetRect.HasArea()
            ? TargetRect
            : new Rect2(Vector2.Zero, Target.Size);
        if (_hasCachedGeometry &&
            targetToViewport == _lastTargetToViewport &&
            proxyParentToViewport == _lastProxyParentToViewport &&
            targetRect == _lastTargetRect)
        {
            return;
        }

        _lastTargetToViewport = targetToViewport;
        _lastProxyParentToViewport = proxyParentToViewport;
        _lastTargetRect = targetRect;
        _hasCachedGeometry = true;

        Transform2D viewportToProxyParent = proxyParentToViewport.AffineInverse();
        Vector2[] corners =
        [
            targetRect.Position,
            new Vector2(targetRect.End.X, targetRect.Position.Y),
            targetRect.End,
            new Vector2(targetRect.Position.X, targetRect.End.Y)
        ];

        Vector2 first = viewportToProxyParent * (targetToViewport * corners[0]);
        float minX = first.X;
        float maxX = first.X;
        float minY = first.Y;
        float maxY = first.Y;

        for (int index = 1; index < corners.Length; index++)
        {
            Vector2 point = viewportToProxyParent *
                (targetToViewport * corners[index]);
            minX = MathF.Min(minX, point.X);
            maxX = MathF.Max(maxX, point.X);
            minY = MathF.Min(minY, point.Y);
            maxY = MathF.Max(maxY, point.Y);
        }

        Vector2 position = new(minX, minY);
        Vector2 size = new(maxX - minX, maxY - minY);
        if (Position != position)
            Position = position;
        if (Size != size)
            Size = size;
    }

    private void SetVisibleIfChanged(bool visible)
    {
        if (Visible != visible)
            Visible = visible;
    }
}
