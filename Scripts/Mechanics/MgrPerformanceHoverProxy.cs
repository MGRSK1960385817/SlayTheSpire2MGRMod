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
            Visible = false;
            return;
        }

        Visible = Target.IsVisibleInTree();
        if (!Visible)
            return;

        if (GetParent() is not CanvasItem proxyParent)
        {
            Visible = false;
            return;
        }

        Transform2D targetToViewport = Target.GetGlobalTransformWithCanvas();
        Transform2D viewportToProxyParent =
            proxyParent.GetGlobalTransformWithCanvas().AffineInverse();
        Rect2 targetRect = TargetRect.HasArea()
            ? TargetRect
            : new Rect2(Vector2.Zero, Target.Size);
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

        Position = new Vector2(minX, minY);
        Size = new Vector2(maxX - minX, maxY - minY);
    }
}
