using Godot;

namespace MGRMod.Mechanics;

/// <summary>
/// Keeps a persistent MGR combat HUD element aligned with a creature while the
/// element itself lives in the native combat UI branch. This preserves the old
/// creature-relative placement without giving the element a global positive Z.
/// </summary>
internal sealed partial class MgrCombatUiFollowAnchor : Node2D
{
    private CanvasItem? _target;
    private Vector2 _targetOffset;

    public bool HasValidTarget =>
        _target is not null && GodotObject.IsInstanceValid(_target);

    public void Initialize(CanvasItem target, Vector2 targetOffset)
    {
        _target = target;
        _targetOffset = targetOffset;
        SetProcess(true);
        SyncTransform();
    }

    public override void _Ready() => SyncTransform();

    public override void _Process(double delta) => SyncTransform();

    private void SyncTransform()
    {
        if (!HasValidTarget ||
            !IsInsideTree() ||
            GetParent() is not CanvasItem parentCanvasItem)
        {
            return;
        }

        Transform2D targetToViewport = _target!.GetGlobalTransformWithCanvas();
        Transform2D parentToViewport =
            parentCanvasItem.GetGlobalTransformWithCanvas();
        Transform2D elementInTargetSpace = new(0f, _targetOffset);
        Transform = parentToViewport.AffineInverse() *
            targetToViewport * elementInTargetSpace;
    }
}
