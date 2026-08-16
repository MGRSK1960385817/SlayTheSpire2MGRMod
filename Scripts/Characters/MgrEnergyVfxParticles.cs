using System.Reflection;
using Godot;
using Godot.Collections;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace MGRMod.Characters;

/// <summary>
/// Reconnects particle children after a mod-local Godot scene is loaded.
/// </summary>
public partial class MgrEnergyVfxParticles : NParticlesContainer
{
    private static readonly FieldInfo ParticlesField =
        typeof(NParticlesContainer).GetField("_particles", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("NParticlesContainer._particles not found");

    public override void _Ready()
    {
        if (ParticlesField.GetValue(this) is not Array<GpuParticles2D> { Count: > 0 })
        {
            var particles = new Array<GpuParticles2D>();
            foreach (var child in GetChildren())
                if (child is GpuParticles2D gpu)
                    particles.Add(gpu);

            ParticlesField.SetValue(this, particles);
        }

        base._Ready();
    }
}
