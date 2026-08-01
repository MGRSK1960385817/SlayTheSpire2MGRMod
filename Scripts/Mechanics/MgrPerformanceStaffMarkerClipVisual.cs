using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Alpha-mask container for the code-drawn staff glyphs. Glyphs can keep their
/// real position while crossing an edge, but only the portion inside the staff
/// rectangle is rendered. This creates a continuous entrance/exit instead of
/// popping the whole symbol on or off.
/// </summary>
internal sealed partial class MgrPerformanceStaffMarkerClipVisual : Node2D
{
    private MgrPerformanceStaffMarkerLayerVisual? _markerLayer;

    public void Initialize(Action<Node2D> drawMarkers)
    {
        ArgumentNullException.ThrowIfNull(drawMarkers);
        ClipChildren = ClipChildrenMode.Only;
        _markerLayer = new MgrPerformanceStaffMarkerLayerVisual
        {
            Name = "ClippedMarkers"
        };
        _markerLayer.Initialize(drawMarkers);
        AddChild(_markerLayer);
        QueueRedraw();
    }

    public void QueueMarkerRedraw()
    {
        QueueRedraw();
        _markerLayer?.QueueRedraw();
    }

    public override void _Draw()
    {
        int lineCount = Math.Max(1, MgrVisualTuning.Performances.StaffLineCount);
        float halfWidth = MgrVisualTuning.Performances.StaffWidth * 0.5f;
        float top = -(lineCount - 1) *
            MgrVisualTuning.Performances.StaffLineSpacing * 0.5f;
        float bottom = top +
            (lineCount - 1) * MgrVisualTuning.Performances.StaffLineSpacing;
        float padding =
            MgrVisualTuning.Performances.StaffMarkerClipVerticalPadding;
        DrawRect(
            new Rect2(
                -halfWidth,
                top - padding,
                halfWidth * 2f,
                bottom - top + padding * 2f),
            Colors.White);
    }
}

internal sealed partial class MgrPerformanceStaffMarkerLayerVisual : Node2D
{
    private Action<Node2D>? _drawMarkers;

    public void Initialize(Action<Node2D> drawMarkers)
    {
        _drawMarkers = drawMarkers;
    }

    public override void _Draw()
    {
        _drawMarkers?.Invoke(this);
    }
}
