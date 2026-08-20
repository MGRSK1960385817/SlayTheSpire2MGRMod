using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace MGRMod.Mechanics;

/// <summary>
/// Owns Light Song's visual for exactly as long as its chained card-selection
/// flow remains active. The lease targets one concrete node, so a stale cleanup
/// cannot remove a later Light Song instance.
/// </summary>
public static class MgrLightSongVfx
{
    public static IDisposable Begin(Player player)
    {
        if (TestMode.IsOn ||
            !LocalContext.IsMe(player) ||
            NGame.Instance?.CurrentRunNode?.GlobalUi is not { } globalUi ||
            NCombatRoom.Instance is not { } room ||
            room.GetCreatureNode(player.Creature) is not { } creatureNode)
        {
            return EmptyLease.Instance;
        }

        // CombatVfxContainer is a foreground combat branch and a positive local
        // Z can overtake vanilla hand/pile selection cards. Keep the persistent
        // rays in normal GlobalUi sibling order immediately before Overlays,
        // matching other full-screen effects that must remain below choices.
        var underlay = new Control
        {
            Name = "MgrLightSongUnderlay",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        underlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        globalUi.AddChildSafely(underlay);
        globalUi.MoveChild(
            underlay,
            Math.Max(0, globalUi.Overlays.GetIndex()));

        var visual = new MgrLightSongBeamVisual();
        underlay.AddChildSafely(visual);
        visual.GlobalPosition = creatureNode.VfxSpawnPosition;
        return new LightSongLease(underlay, visual);
    }

    private sealed class LightSongLease(
        Control underlay,
        MgrLightSongBeamVisual visual) : IDisposable
    {
        private Control? _underlay = underlay;
        private MgrLightSongBeamVisual? _visual = visual;

        public void Dispose()
        {
            if (_visual is { } current &&
                GodotObject.IsInstanceValid(current))
            {
                current.Finish();
            }
            else if (_underlay is { } currentUnderlay &&
                     GodotObject.IsInstanceValid(currentUnderlay))
            {
                currentUnderlay.QueueFree();
            }

            _visual = null;
            _underlay = null;
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

internal sealed partial class MgrLightSongBeamVisual : Node2D
{
    private static readonly Color[] Palette =
    [
        new("fff4bc"),
        new("ffe08a"),
        new("bfeaff"),
        new("e8d2ff"),
        new("ffffff")
    ];

    private float _age;
    private float _finishAge;
    private bool _finishing;

    public override void _Ready()
    {
        ZIndex = MgrVisualTuning.LightSongVfx.ZIndex;
        SetProcess(true);
    }

    public void Finish()
    {
        if (_finishing)
            return;

        _finishing = true;
        _finishAge = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float elapsed = (float)delta;
        _age += elapsed;
        if (_finishing)
        {
            _finishAge += elapsed;
            if (_finishAge >= MgrVisualTuning.LightSongVfx.FadeOutSeconds)
            {
                if (GetParent() is { } underlay)
                    underlay.QueueFree();
                else
                    QueueFree();
                return;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float entryProgress = Math.Clamp(
            _age / MgrVisualTuning.LightSongVfx.EntryBurstSeconds,
            0f,
            1f);
        float entryStrength = 1f - entryProgress;
        float finishProgress = _finishing
            ? Math.Clamp(
                _finishAge / MgrVisualTuning.LightSongVfx.FadeOutSeconds,
                0f,
                1f)
            : 0f;
        float fade = _finishing
            ? 1f - finishProgress
            : 1f;
        float finishFlare = _finishing
            ? MathF.Sin(finishProgress * MathF.PI)
            : 0f;
        float breathing = 0.86f + MathF.Sin(_age * 3.1f) * 0.14f;
        float phase = _age * MgrVisualTuning.LightSongVfx.RotationRadiansPerSecond;
        float viewportLength = GetViewportRect().Size.Length() *
            MgrVisualTuning.LightSongVfx.BeamLengthScale;

        int beamCount = MgrVisualTuning.LightSongVfx.BeamCount;
        for (int index = 0; index < beamCount; index++)
        {
            float angle = phase + index * Mathf.Tau / beamCount;
            Vector2 direction = Vector2.FromAngle(angle);
            Vector2 normal = new(-direction.Y, direction.X);
            float sequence = 0.52f + 0.48f * Math.Max(
                0f,
                MathF.Sin(_age * 5.4f - index * 0.82f));
            float beamAlpha =
                (MgrVisualTuning.LightSongVfx.BaseBeamAlpha * breathing * sequence +
                 entryStrength * 0.075f +
                 finishFlare * 0.045f) * fade;
            float startHalfWidth =
                MgrVisualTuning.LightSongVfx.NearHalfWidth *
                (1f + entryStrength * 0.30f);
            float endHalfWidth =
                MgrVisualTuning.LightSongVfx.FarHalfWidth *
                (1f + entryStrength * 0.42f + finishFlare * 0.18f);
            Vector2 start = direction * 22f;
            Vector2 end = direction * viewportLength;
            Color beamColor = Palette[index % Palette.Length] with
            {
                A = beamAlpha
            };

            DrawPolygon(
                [
                    start - normal * startHalfWidth,
                    start + normal * startHalfWidth,
                    end + normal * endHalfWidth,
                    end - normal * endHalfWidth
                ],
                [beamColor, beamColor, beamColor, beamColor]);

            Color coreColor = Palette[(index + 1) % Palette.Length] with
            {
                A = beamAlpha * 2.25f
            };
            DrawLine(
                start,
                end,
                coreColor,
                1.4f + entryStrength * 1.1f,
                true);

            // A short bright packet continuously travels away from the
            // character, making the persistent rays read as emitted light
            // rather than a static wheel laid over the battlefield.
            float travel = (_age * MgrVisualTuning.LightSongVfx.PacketSpeed +
                            index / (float)beamCount) % 1f;
            float packetCenter = Mathf.Lerp(54f, viewportLength * 0.92f, travel);
            float packetHalfLength = Mathf.Lerp(22f, 68f, travel);
            float packetFade = MathF.Sin(travel * MathF.PI) * fade;
            DrawLine(
                direction * (packetCenter - packetHalfLength),
                direction * (packetCenter + packetHalfLength),
                Palette[(index + 2) % Palette.Length] with
                {
                    A = packetFade * 0.34f
                },
                3.0f,
                true);
        }

        DrawOriginHalo(entryStrength, finishFlare, fade, phase);
    }

    private void DrawOriginHalo(
        float entryStrength,
        float finishFlare,
        float fade,
        float phase)
    {
        float pulse = 0.88f + MathF.Sin(_age * 4.2f) * 0.12f;
        float outerRadius = 58f + entryStrength * 34f + finishFlare * 22f;
        DrawCircle(
            Vector2.Zero,
            34f + finishFlare * 12f,
            new Color(1f, 0.90f, 0.52f, 0.095f * pulse * fade));
        DrawArc(
            Vector2.Zero,
            outerRadius,
            phase * 0.72f,
            phase * 0.72f + MathF.PI * 1.54f,
            54,
            new Color(1f, 0.94f, 0.68f, 0.68f * fade),
            2.6f,
            true);
        DrawArc(
            Vector2.Zero,
            outerRadius * 0.72f,
            -phase * 0.46f,
            -phase * 0.46f + MathF.PI * 1.36f,
            48,
            new Color(0.72f, 0.90f, 1f, 0.54f * fade),
            1.8f,
            true);

        for (int index = 0; index < 8; index++)
        {
            float angle = phase * 1.3f + index * Mathf.Tau / 8f;
            Vector2 center = Vector2.FromAngle(angle) *
                (outerRadius + 12f + index % 2 * 8f);
            float size = 3.2f + index % 3;
            Color sparkle = Palette[index % Palette.Length] with
            {
                A = (0.48f + entryStrength * 0.34f) * fade
            };
            DrawLine(
                center - Vector2.Right * size,
                center + Vector2.Right * size,
                sparkle,
                1.4f,
                true);
            DrawLine(
                center - Vector2.Up * size,
                center + Vector2.Up * size,
                sparkle,
                1.4f,
                true);
        }
    }
}
