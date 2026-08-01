using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SlayTheSpire2MGRMod.Characters;

/// <summary>
/// Mod-local energy counter root. Both the layered orb and its burst now use
/// MGR-owned artwork from the character asset directory.
/// </summary>
public partial class MgrEnergyCounter : NEnergyCounter
{
    private readonly List<(Control Layer, float DegreesPerSecond)> _rotatingLayers = [];

    public override void _Ready()
    {
        base._Ready();

        // Preserve the original MGR energy-orb motion. Tower 1 supplied these
        // speeds in layer5 -> layer1 order; layer0 is the static foundation.
        RegisterRotatingLayer("Layers/RotationLayers/Layer5", -60f);
        RegisterRotatingLayer("Layers/RotationLayers/Layer4", 60f);
        RegisterRotatingLayer("Layers/RotationLayers/Layer3", -40f);
        RegisterRotatingLayer("Layers/RotationLayers/Layer2", 60f);
        RegisterRotatingLayer("Layers/RotationLayers/Layer1", 360f);

        var burstColor = new Color(0.964706f, 0.611765f, 0.768627f, 0.6f);
        if (GetNodeOrNull<CpuParticles2D>("%BurstBack") is { } back)
            back.Color = burstColor;
        if (GetNodeOrNull<CpuParticles2D>("%BurstFront") is { } front)
            front.Color = burstColor;
    }

    public override void _Process(double delta)
    {
        // NEnergyCounter's default loop rotates children according to their
        // sibling index. MGR instead preserves Tower 1's explicit per-layer
        // speeds, so calling the base loop here would rotate every layer twice
        // and distort the intended relative motion.
        float elapsed = (float)delta;
        foreach ((Control layer, float degreesPerSecond) in _rotatingLayers)
        {
            if (!GodotObject.IsInstanceValid(layer))
                continue;

            layer.Rotation = MathF.IEEERemainder(
                layer.Rotation + Mathf.DegToRad(degreesPerSecond) * elapsed,
                MathF.Tau);
        }
    }

    private void RegisterRotatingLayer(string path, float degreesPerSecond)
    {
        if (GetNodeOrNull<Control>(path) is { } layer)
            _rotatingLayers.Add((layer, degreesPerSecond));
    }
}
