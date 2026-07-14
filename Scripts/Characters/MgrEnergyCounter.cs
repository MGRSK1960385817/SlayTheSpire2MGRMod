using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SlayTheSpire2MGRMod.Characters;

/// <summary>
/// Mod-local energy counter root. The scene layout currently uses temporary
/// WineFox artwork, while the type and behavior remain owned by MGR.
/// </summary>
public partial class MgrEnergyCounter : NEnergyCounter
{
    public override void _Ready()
    {
        base._Ready();

        var burstColor = new Color(0.964706f, 0.611765f, 0.768627f, 0.6f);
        if (GetNodeOrNull<CpuParticles2D>("%BurstBack") is { } back)
            back.Color = burstColor;
        if (GetNodeOrNull<CpuParticles2D>("%BurstFront") is { } front)
            front.Color = burstColor;
    }
}
