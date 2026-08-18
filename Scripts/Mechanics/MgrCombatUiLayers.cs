using Godot;

namespace MGRMod.Mechanics;

/// <summary>
/// Owns MGR's persistent combat-HUD draw order inside the native free-position
/// card UI container. Note visuals are always inserted before Performance
/// visuals, so a Performance rack wins any overlap without non-zero Z indices.
/// </summary>
internal static class MgrCombatUiLayers
{
    private const string RootName = "MgrCombatPresentationLayers";
    private const string NoteLayerName = "MgrNoteLayer";
    private const string PerformanceLayerName = "MgrPerformanceLayer";
    private const string PerformanceAmbientLayerName = "PerformanceAmbient";
    private const string PerformanceRackLayerName = "PerformanceRack";

    public static Control GetNoteLayer(Control nativeParent) =>
        GetLayer(nativeParent, NoteLayerName);

    public static Control GetPerformanceLayer(Control nativeParent) =>
        GetLayer(nativeParent, PerformanceLayerName)
            .GetNode<Control>(PerformanceRackLayerName);

    public static Control GetPerformanceAmbientLayer(Control nativeParent) =>
        GetLayer(nativeParent, PerformanceLayerName)
            .GetNode<Control>(PerformanceAmbientLayerName);

    private static Control GetLayer(Control nativeParent, string layerName)
    {
        Control root = nativeParent.GetNodeOrNull<Control>(RootName) ??
            CreateRoot(nativeParent);
        return root.GetNode<Control>(layerName);
    }

    private static Control CreateRoot(Control nativeParent)
    {
        var root = new Control
        {
            Name = RootName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        nativeParent.AddChild(root);

        // Same-Z CanvasItems draw in tree order. This order is therefore the
        // cross-system contract and must not depend on feature creation timing.
        root.AddChild(new Control
        {
            Name = NoteLayerName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var performanceLayer = new Control
        {
            Name = PerformanceLayerName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(performanceLayer);
        performanceLayer.AddChild(new Control
        {
            Name = PerformanceAmbientLayerName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        performanceLayer.AddChild(new Control
        {
            Name = PerformanceRackLayerName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        return root;
    }
}
