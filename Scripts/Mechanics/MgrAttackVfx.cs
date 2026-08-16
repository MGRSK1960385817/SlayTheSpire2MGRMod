using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Thin factory for reusable MGR attack VFX concerns: safe instancing,
/// per-instance tinting and bounded damage-driven scale. Card-specific timing
/// and sequencing intentionally remain on the individual card.
/// </summary>
public static class MgrAttackVfx
{
    private const string GiantHorizontalSlashPath =
        "vfx/vfx_giant_horizontal_slash";

    public static readonly Color DefaultFireTint = new("ff8b57");
    public static readonly Color StarPurple = new("c88cff");
    public static readonly Color StarGold = new("ffd27a");
    public static readonly Color FlyingSlashBlue = new("62b8ff");
    public static readonly Color CursePurple = new("67265f");
    public static readonly Color CurseDarkRed = new("6f1728");
    // Nearly-black burgundy remains readable inside the native additive fire
    // texture while presenting as black flame rather than an ordinary red hit.
    public static readonly Color CurseBlackFlame = new("19040d");

    /// <summary>
    /// Grows once per damage doubling and clamps the result so cards whose
    /// damage scales indefinitely cannot cover the entire combat screen.
    /// Values at or below the reference damage retain the base scale.
    /// </summary>
    public static float ScaleByDamage(
        decimal damage,
        decimal referenceDamage,
        float baseScale,
        float growthPerDoubling,
        float maxScale)
    {
        if (referenceDamage <= 0m || damage <= referenceDamage)
            return baseScale;

        double ratio = (double)(damage / referenceDamage);
        float scale = baseScale +
            growthPerDoubling * (float)Math.Log2(ratio);
        return Mathf.Clamp(scale, baseScale, maxScale);
    }

    public static Node2D? CreateHorizontalSlash(
        Creature target,
        Color tint,
        float scale)
    {
        if (TestMode.IsOn)
            return null;

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null)
            return null;

        string scenePath = SceneHelper.GetScenePath(GiantHorizontalSlashPath);
        Node2D vfx = PreloadManager.Cache
            .GetScene(scenePath)
            .Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
        vfx.GlobalPosition = creatureNode.VfxSpawnPosition;
        vfx.Modulate = tint;
        vfx.Scale = Vector2.One * Math.Max(0.01f, scale);
        return vfx;
    }

    public static NFireBurstVfx? CreateFireBurst(
        Creature target,
        Color tint,
        float scale)
    {
        if (TestMode.IsOn)
            return null;

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        return creatureNode is null
            ? null
            : NFireBurstVfx.Create(
                creatureNode.GetBottomOfHitbox(),
                Math.Max(0.01f, scale),
                tint);
    }

    public static void SpawnFireBurst(
        Creature target,
        Color tint,
        float scale)
    {
        NFireBurstVfx? vfx = CreateFireBurst(target, tint, scale);
        if (vfx is null || NCombatRoom.Instance is null)
            return;

        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(vfx);
    }

    public static NBigSlashVfx? CreateBigSlash(
        Creature target,
        Color tint,
        float scale)
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null)
            return null;

        NBigSlashVfx? vfx = NBigSlashVfx.Create(
            creatureNode.VfxSpawnPosition,
            target.IsEnemy,
            tint);
        if (vfx is not null)
            vfx.Scale *= Math.Max(0.01f, scale);
        return vfx;
    }

    public static NBigSlashImpactVfx? CreateBigSlashImpact(
        Creature target,
        Color tint,
        float scale,
        float rotationDegrees = 60f)
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null)
            return null;

        NBigSlashImpactVfx? vfx = NBigSlashImpactVfx.Create(
            creatureNode.VfxSpawnPosition,
            rotationDegrees,
            tint);
        if (vfx is not null)
            vfx.Scale *= Math.Max(0.01f, scale);
        return vfx;
    }

    public static NGaseousImpactVfx? CreateGaseousImpact(
        Creature target,
        Color tint,
        float scale = 1f)
    {
        NGaseousImpactVfx? vfx = NGaseousImpactVfx.Create(target, tint);
        if (vfx is not null)
            vfx.Scale *= Math.Max(0.01f, scale);
        return vfx;
    }

    public static Node2D? CreateStarryImpact(
        Creature target,
        Color tint,
        float scale = 1f) =>
        CreateTintedSceneAtTarget(
            target,
            VfxCmd.starryImpactVfx,
            tint,
            scale);

    public static Node2D? CreateFlyingSlash(
        Creature target,
        Color tint,
        float scale = 1f,
        bool reverse = false)
    {
        Node2D? vfx = CreateTintedSceneAtTarget(
            target,
            VfxCmd.flyingSlashPath,
            tint,
            scale);
        if (vfx is not null && reverse)
            vfx.Scale = new Vector2(-MathF.Abs(vfx.Scale.X), vfx.Scale.Y);
        return vfx;
    }

    public static async Task PlayGrandFinaleFlourish(
        Creature performer,
        IEnumerable<Creature> targets)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        NGrandFinaleVfx? anticipation = NGrandFinaleVfx.Create(performer);
        if (anticipation is not null)
        {
            room.CombatVfxContainer.AddChildSafely(anticipation);
            await Cmd.Wait(NGrandFinaleVfx.totalAnticipationDuration);
        }

        foreach (Creature target in targets.Where(target => target.IsAlive))
        {
            NGrandFinaleImpactVfx? impact = NGrandFinaleImpactVfx.Create(target);
            if (impact is not null)
                room.CombatVfxContainer.AddChildSafely(impact);
        }
    }

    public static MgrGunshotVfx? CreateGunshot(
        Creature attacker,
        Creature target,
        Color tint,
        float scale = 1f) =>
        MgrGunshotVfx.Create(attacker, target, tint, scale);

    public static void SpawnFishRush(
        Creature attacker,
        Creature target,
        float scale = 1f)
    {
        if (NCombatRoom.Instance is not { } room)
            return;

        MgrFishVfx? vfx = MgrFishVfx.Create(attacker, target, scale);
        if (vfx is not null)
            room.CombatVfxContainer.AddChildSafely(vfx);
    }

    public static async Task PlaySmallMagicMissile(
        CardModel sourceCard,
        Creature target,
        Color tint)
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null || NCombatRoom.Instance is null)
            return;

        NSmallMagicMissileVfx? vfx = NSmallMagicMissileVfx.Create(
            creatureNode.GetBottomOfHitbox(),
            tint);
        if (vfx is null)
            return;

        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(vfx);
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            vfx.WaitTime));
    }

    public static async Task PlayLargeMagicMissile(
        CardModel sourceCard,
        Creature target,
        Color tint,
        float scale = 1f)
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null || NCombatRoom.Instance is null)
            return;

        NLargeMagicMissileVfx? vfx = NLargeMagicMissileVfx.Create(
            creatureNode.GetBottomOfHitbox(),
            tint);
        if (vfx is null)
            return;

        vfx.Scale *= Math.Max(0.01f, scale);

        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(vfx);
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            vfx.WaitTime));
    }

    public static async Task PlaySweepingBeam(
        CardModel sourceCard,
        Creature attacker,
        List<Creature> targets,
        Color tint,
        float particleSizeScale = 1f)
    {
        if (targets.Count == 0 || NCombatRoom.Instance is null)
            return;

        NSweepingBeamVfx? vfx = NSweepingBeamVfx.Create(attacker, targets);
        if (vfx is null)
            return;

        // Root modulation colors the beam body. The original impact sparks keep
        // a small amount of their native blue, producing a purple-blue prism.
        vfx.Modulate = tint;
        ScaleParticleSprites(vfx, Math.Max(0.12f, particleSizeScale));
        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(vfx);
        await Cmd.Wait(MgrPerformanceSystem.GetVisualWaitDuration(
            sourceCard,
            0.38f));
    }

    private static void ScaleParticleSprites(Node root, float scale)
    {
        foreach (Node node in root.FindChildren(
                     "*",
                     "GPUParticles2D",
                     recursive: true,
                     owned: false))
        {
            if (node is not GpuParticles2D particles ||
                particles.ProcessMaterial is not ParticleProcessMaterial source)
            {
                continue;
            }

            // Duplicate each material before modifying it: cached vanilla scene
            // resources may be shared by later effects and must stay untouched.
            var material = (ParticleProcessMaterial)source.Duplicate();
            material.ScaleMin *= scale;
            material.ScaleMax *= scale;
            particles.ProcessMaterial = material;
        }
    }

    private static Node2D? CreateTintedSceneAtTarget(
        Creature target,
        string resourcePath,
        Color tint,
        float scale)
    {
        if (TestMode.IsOn)
            return null;

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null)
            return null;

        string scenePath = SceneHelper.GetScenePath(resourcePath);
        Node2D vfx = PreloadManager.Cache
            .GetScene(scenePath)
            .Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
        vfx.GlobalPosition = creatureNode.VfxSpawnPosition;
        vfx.Modulate = tint;
        vfx.Scale = Vector2.One * Math.Max(0.01f, scale);
        return vfx;
    }
}
