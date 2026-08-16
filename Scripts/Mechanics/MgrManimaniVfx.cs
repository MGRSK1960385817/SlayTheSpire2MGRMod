using Godot;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MGRMod.Characters;

namespace MGRMod.Mechanics;

/// <summary>
/// Manimani's two-layer presentation: a non-overlapping full-screen frame
/// sequence above the played-card display, followed by a target-local burst.
/// Gameplay waits for m1-m4, then starts damage on the exact beat where m6
/// becomes visible.
/// </summary>
public static class MgrManimaniVfx
{
    private const string FatalFireSound = "STS_SFX_BurnCard_v1.mp3";
    private const string FatalGaseousImpactSound = "hiss.mp3";
    private const string FatalGunshotImpactSound = "blunt_attack.mp3";

    private static readonly string[] TexturePaths =
    [
        $"{Entry.ResPath}/images/vfx/m1.png",
        $"{Entry.ResPath}/images/vfx/m2.png",
        $"{Entry.ResPath}/images/vfx/m3.png",
        $"{Entry.ResPath}/images/vfx/m4.png",
        $"{Entry.ResPath}/images/vfx/m6.png"
    ];

    private static readonly float[] PreludeDurations =
    [
        MgrVisualTuning.ManimaniVfx.Frame1Seconds,
        MgrVisualTuning.ManimaniVfx.Frame2Seconds,
        MgrVisualTuning.ManimaniVfx.Frame3Seconds,
        MgrVisualTuning.ManimaniVfx.Frame4Seconds
    ];

    public static async Task PlayPrelude(bool fatalConditionSatisfied)
    {
        if (!fatalConditionSatisfied ||
            TestMode.IsOn ||
            NCombatRoom.Instance is null ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi)
        {
            return;
        }

        Texture2D[]? textures = LoadTextures();
        if (textures is null)
            return;

        var backdrop = new MgrManimaniBackdropVfx();
        backdrop.Initialize(textures);
        // A dedicated CanvasLayer is required here: combat VFX containers are
        // rendered below the native played-card presentation, whose large card
        // otherwise sits over the face in the full-screen source artwork.
        globalUi.AddChildSafely(backdrop);

        for (int frame = 0; frame < PreludeDurations.Length; frame++)
        {
            if (!GodotObject.IsInstanceValid(backdrop) ||
                !backdrop.IsInsideTree())
            {
                return;
            }

            backdrop.ShowFrame(frame);
            await Cmd.Wait(PreludeDurations[frame]);
        }

        if (!GodotObject.IsInstanceValid(backdrop) ||
            !backdrop.IsInsideTree())
        {
            return;
        }

        // A single Sprite2D changes texture in place, so adjacent source images
        // can never overlap. This sequence is entered only for the fully
        // satisfied Fatal condition, whose outcome frame is m6.
        backdrop.ShowOutcome(4);
        // Return immediately so the caller resolves damage on the same beat
        // that m6 appears and starts fading.
    }

    public static void SpawnImpact(
        Creature target,
        bool fatalConditionSatisfied)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        NCreature? creatureNode = room.GetCreatureNode(target);
        if (creatureNode is null)
            return;

        var impact = new MgrManimaniImpactVfx();
        impact.Initialize(fatalConditionSatisfied);
        impact.GlobalPosition = creatureNode.VfxSpawnPosition;
        room.CombatVfxContainer.AddChildSafely(impact);

        // Ordinary hits keep the note-generation cue. The fully satisfied
        // execution replaces only that cue with gaseous and gunshot impact
        // layers; its existing fire cue remains below.
        if (fatalConditionSatisfied)
        {
            NDebugAudioManager.Instance?.Play(
                FatalGaseousImpactSound,
                MgrVisualTuning.ManimaniVfx.FatalGaseousImpactSoundVolume);
            NDebugAudioManager.Instance?.Play(
                FatalGunshotImpactSound,
                MgrVisualTuning.ManimaniVfx.FatalGunshotImpactSoundVolume);
        }
        else
        {
            MgrAudio.PlayNoteChannel(
                MgrVisualTuning.ManimaniVfx.ImpactNoteSoundVolume);
        }

        // Spawn on the same visual beat as the custom shock rings and shards.
        // The native burst supplies a hard flame body without reintroducing the
        // soft starry impact that Manimani previously used.
        MgrAttackVfx.SpawnFireBurst(
            target,
            MgrVisualTuning.ManimaniVfx.ImpactFireColor,
            fatalConditionSatisfied
                ? MgrVisualTuning.ManimaniVfx.FatalImpactFireScale
                : MgrVisualTuning.ManimaniVfx.ImpactFireScale);

        if (fatalConditionSatisfied)
        {
            NDebugAudioManager.Instance?.Play(
                FatalFireSound,
                MgrVisualTuning.ManimaniVfx.FatalFireSoundVolume);
        }
    }

    private static Texture2D[]? LoadTextures()
    {
        var textures = new Texture2D[TexturePaths.Length];
        for (int index = 0; index < TexturePaths.Length; index++)
        {
            Texture2D? texture = ResourceLoader.Load<Texture2D>(
                TexturePaths[index]);
            if (texture is null ||
                texture.GetWidth() <= 0 ||
                texture.GetHeight() <= 0)
            {
                Entry.Logger.Warn(
                    $"Missing Manimani VFX texture: {TexturePaths[index]}");
                return null;
            }

            textures[index] = texture;
        }

        return textures;
    }
}

internal sealed partial class MgrManimaniBackdropVfx : CanvasLayer
{
    private Texture2D[] _textures = [];
    private Sprite2D? _sprite;

    public void Initialize(Texture2D[] textures) => _textures = textures;

    public override void _Ready()
    {
        Layer = MgrVisualTuning.ManimaniVfx.BackdropCanvasLayer;
        _sprite = new Sprite2D
        {
            Centered = true,
            Modulate = new Color(
                MgrVisualTuning.ManimaniVfx.BackdropBrightness,
                MgrVisualTuning.ManimaniVfx.BackdropBrightness,
                MgrVisualTuning.ManimaniVfx.BackdropBrightness,
                MgrVisualTuning.ManimaniVfx.Frame1Opacity)
        };
        AddChild(_sprite);
        ShowFrame(0);
    }

    public void ShowFrame(int frameIndex)
    {
        if (_sprite is null ||
            frameIndex < 0 ||
            frameIndex >= _textures.Length)
        {
            return;
        }

        Texture2D texture = _textures[frameIndex];
        _sprite.Texture = texture;
        _sprite.Modulate = _sprite.Modulate with
        {
            A = GetFrameOpacity(frameIndex)
        };

        Rect2 viewport = GetViewport().GetVisibleRect();
        _sprite.Position = viewport.GetCenter() +
            MgrVisualTuning.ManimaniVfx.BackdropOffset;
        float coverScale = Math.Max(
            viewport.Size.X / texture.GetWidth(),
            viewport.Size.Y / texture.GetHeight());
        _sprite.Scale = Vector2.One * coverScale *
            MgrVisualTuning.ManimaniVfx.BackdropScale;
    }

    private static float GetFrameOpacity(int frameIndex) => frameIndex switch
    {
        0 => MgrVisualTuning.ManimaniVfx.Frame1Opacity,
        1 => MgrVisualTuning.ManimaniVfx.Frame2Opacity,
        2 => MgrVisualTuning.ManimaniVfx.Frame3Opacity,
        3 => MgrVisualTuning.ManimaniVfx.Frame4Opacity,
        _ => MgrVisualTuning.ManimaniVfx.OutcomeOpacity
    };

    public void ShowOutcome(int frameIndex)
    {
        ShowFrame(frameIndex);
        Tween tween = CreateTween();
        tween.TweenProperty(
                _sprite,
                "modulate:a",
                0f,
                MgrVisualTuning.ManimaniVfx.OutcomeFadeSeconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}

/// <summary>
/// A hard radial detonation built from a white-hot core, two shock rings and
/// fast angular fragments. It intentionally avoids slash and blunt silhouettes.
/// </summary>
internal sealed partial class MgrManimaniImpactVfx : Node2D
{
    private readonly List<ImpactShard> _shards = [];
    private float _age;
    private float _effectScale;

    private sealed record ImpactShard(
        float Angle,
        float Speed,
        float Length,
        float Width,
        float Spin,
        float Delay,
        Color Color);

    public void Initialize(bool fatalConditionSatisfied)
    {
        _effectScale = MgrVisualTuning.ManimaniVfx.ImpactScale *
            (fatalConditionSatisfied
                ? MgrVisualTuning.ManimaniVfx.FatalImpactScale
                : 1f);
        ZIndex = MgrVisualTuning.ManimaniVfx.ImpactZIndex;

        int count = MgrVisualTuning.ManimaniVfx.ImpactShardCount;
        for (int index = 0; index < count; index++)
        {
            float angle = index * Mathf.Tau / count +
                RandomRange(-0.11f, 0.11f);
            Color color = (index % 4) switch
            {
                0 => new Color("fff0c7"),
                1 => new Color("ff5a37"),
                2 => new Color("b20e24"),
                _ => new Color("371020")
            };
            _shards.Add(new ImpactShard(
                angle,
                RandomRange(300f, 540f),
                RandomRange(18f, 42f),
                RandomRange(5f, 12f),
                RandomRange(-8f, 8f),
                RandomRange(0f, 0.045f),
                color));
        }

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= MgrVisualTuning.ManimaniVfx.ImpactLifetimeSeconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float lifetime = MgrVisualTuning.ManimaniVfx.ImpactLifetimeSeconds;
        float progress = Math.Clamp(_age / lifetime, 0f, 1f);
        float impactEnvelope = 1f - progress;

        // The core collapses quickly while the two rings continue expanding,
        // giving the hit a sharp detonation instead of a soft lingering puff.
        float flash = Math.Clamp(1f - _age / 0.11f, 0f, 1f);
        DrawCircle(
            Vector2.Zero,
            (24f + flash * 64f) * _effectScale,
            new Color(1f, 0.88f, 0.70f, flash * 0.94f));
        DrawCircle(
            Vector2.Zero,
            (18f + flash * 42f) * _effectScale,
            Colors.White with { A = flash });

        float firstRadius = Mathf.Lerp(18f, 174f, EaseOut(progress)) *
            _effectScale;
        float secondProgress = Math.Clamp((progress - 0.08f) / 0.92f, 0f, 1f);
        float secondRadius = Mathf.Lerp(10f, 122f, EaseOut(secondProgress)) *
            _effectScale;
        DrawArc(
            Vector2.Zero,
            firstRadius,
            0f,
            Mathf.Tau,
            64,
            new Color(1f, 0.24f, 0.12f, impactEnvelope * 0.86f),
            7f * _effectScale,
            true);
        DrawArc(
            Vector2.Zero,
            secondRadius,
            0f,
            Mathf.Tau,
            48,
            new Color(1f, 0.83f, 0.50f, impactEnvelope * 0.72f),
            3f * _effectScale,
            true);

        foreach (ImpactShard shard in _shards)
        {
            float localAge = _age - shard.Delay;
            if (localAge < 0f)
                continue;

            float shardProgress = Math.Clamp(
                localAge / (lifetime - shard.Delay),
                0f,
                1f);
            float alpha = MathF.Pow(1f - shardProgress, 0.62f);
            float distance = (12f + shard.Speed * localAge) * _effectScale;
            Vector2 center = Vector2.FromAngle(shard.Angle) * distance;
            float rotation = shard.Angle + shard.Spin * localAge;
            float length = shard.Length * _effectScale;
            float width = shard.Width * _effectScale *
                (1f - shardProgress * 0.52f);

            DrawSetTransform(center, rotation, Vector2.One);
            Color color = shard.Color with { A = alpha * 0.94f };
            DrawPolygon(
                [
                    new Vector2(length * 0.68f, 0f),
                    new Vector2(-length * 0.42f, width),
                    new Vector2(-length * 0.58f, -width * 0.72f)
                ],
                [color, color, color]);
        }

        DrawSetTransform(Vector2.Zero);
    }

    private static float EaseOut(float value) =>
        1f - MathF.Pow(1f - value, 3f);

    private static float RandomRange(float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, Random.Shared.NextSingle());
}
