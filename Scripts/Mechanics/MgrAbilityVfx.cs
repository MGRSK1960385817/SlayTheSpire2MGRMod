using Godot;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.TestSupport;
using MGRMod.Cards;

namespace MGRMod.Mechanics;

/// <summary>
/// Non-blocking, texture-free presentation shared by MGR skills and powers.
/// The helper deliberately owns only visual concerns: card and power models
/// retain their gameplay timing, while every spawned node frees itself.
/// </summary>
public static class MgrAbilityVfx
{
    private const string BloodyImpactPath = "vfx/vfx_bloody_impact";
    private const float DaybreakGatherSeconds = 0.28f;
    private const float DaybreakHoldSeconds = 0.22f;
    private const float DaybreakExhaustSeconds = 0.58f;
    private const float DaybreakExhaustStaggerSeconds = 0.035f;

    public static void PlayOfferingBlood(Creature target)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is null)
            return;

        // Offering reaches the same feedback through Tower 2's ordinary self-
        // damage presentation. Calling the underlying blood impact explicitly
        // keeps Crime and Punishment/Frenzy readable even when their ValueProps
        // intentionally differ from Offering's.
        VfxCmd.PlayOnCreatureCenter(target, BloodyImpactPath);
    }

    /// <summary>
    /// Gives every active MGR gold Skill/Power a short semantic cast flourish.
    /// Direct-damage gold Attacks retain their card-specific attack VFX instead.
    /// </summary>
    public static void PlayGoldCardCast(CardModel card)
    {
        if (TestMode.IsOn ||
            NCombatRoom.Instance is null ||
            card.Type is not (CardType.Skill or CardType.Power))
        {
            return;
        }

        MgrAbilityVfxStyle style = card switch
        {
            CanonForm => MgrAbilityVfxStyle.Echo,
            ChaosMagic => MgrAbilityVfxStyle.Wheel,
            CrimeAndPunishment => MgrAbilityVfxStyle.Blood,
            DualLovers => MgrAbilityVfxStyle.Dual,
            Higan => MgrAbilityVfxStyle.Horizon,
            WatchingU => MgrAbilityVfxStyle.Seal,
            Prismatic => MgrAbilityVfxStyle.Prism,
            SatelliteGirl => MgrAbilityVfxStyle.Satellite,
            SixthSense => MgrAbilityVfxStyle.Eye,
            ImagineCreate => MgrAbilityVfxStyle.Creation,
            DaybreakFrontline => MgrAbilityVfxStyle.Dawn,
            FlawedGirl => MgrAbilityVfxStyle.Glitch,
            GalaxyLamp => MgrAbilityVfxStyle.Galaxy,
            LightSong => MgrAbilityVfxStyle.Score,
            MeteorAftermath => MgrAbilityVfxStyle.Meteor,
            OtomeDissection => MgrAbilityVfxStyle.Cut,
            PleasingGhosts => MgrAbilityVfxStyle.Ghost,
            Resonate => MgrAbilityVfxStyle.Resonance,
            Omnia => MgrAbilityVfxStyle.Prism,
            _ => MgrAbilityVfxStyle.Score
        };

        SpawnCastBurst(card.Owner.Creature, style);
    }

    /// <summary>
    /// Blue cards only opt in when their effect has a strong visual identity.
    /// Ordinary block/draw and Note/Performance cards keep the native/system UI
    /// instead of turning every mid-rarity play into the same particle burst.
    /// </summary>
    public static void PlayFeaturedUncommonCardCast(CardModel card)
    {
        MgrAbilityVfxStyle? style = card switch
        {
            CosmoSpice => MgrAbilityVfxStyle.Prism,
            ElectricAngel => MgrAbilityVfxStyle.Electric,
            MindMirage => MgrAbilityVfxStyle.Mirage,
            StainedNocturne => MgrAbilityVfxStyle.Nocturne,
            StarfallSea => MgrAbilityVfxStyle.Galaxy,
            PaleDread => MgrAbilityVfxStyle.Ghost,
            WhiteSouthWind => MgrAbilityVfxStyle.Wind,
            RainbowScale => MgrAbilityVfxStyle.Prism,
            _ => null
        };

        if (style is { } resolved)
            SpawnCastBurst(card.Owner.Creature, resolved, 0.84f);
    }

    public static void SpawnCastBurst(
        Creature target,
        MgrAbilityVfxStyle style,
        float scale = 1f)
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (creatureNode is null || NCombatRoom.Instance is null)
            return;

        var visual = new MgrAbilityBurstVisual();
        visual.Initialize(style, Math.Max(0.1f, scale));
        visual.GlobalPosition = creatureNode.VfxSpawnPosition + new Vector2(0f, -18f);
        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(visual);
    }

    /// <summary>
    /// Daybreak Frontline first gathers every affected card into the middle,
    /// then passes those temporary card faces through Tower 2's full Fiend
    /// Fire-style exhaust dissolve. Gameplay pile movement remains in the
    /// caller, so hooks and pile bookkeeping still resolve exactly once.
    /// </summary>
    public static async Task PlayCentralCardExhaust(
        IReadOnlyList<CardModel> cards)
    {
        if (TestMode.IsOn ||
            NCombatRoom.Instance is not { } room ||
            cards.Count == 0)
        {
            return;
        }

        Control visualRoot = room.Ui.MessyCardPreviewContainer;
        Vector2 center = room.GetViewportRect().Size * 0.5f;
        List<NCard> cardNodes = [];
        int visibleCount = Math.Min(cards.Count, 24);

        for (int index = 0; index < visibleCount; index++)
        {
            NCard? node = NCard.Create(cards[index]);
            if (node is null)
                continue;

            visualRoot.AddChildSafely(node);
            node.UpdateVisuals(
                cards[index].Pile?.Type ?? PileType.Play,
                CardPreviewMode.Normal);
            node.MouseFilter = Control.MouseFilterEnum.Ignore;
            node.ZIndex = 120 + index;
            node.GlobalPosition = GetDaybreakCardOrigin(
                cards[index],
                node,
                center,
                index);
            node.Scale = Vector2.One * 0.22f;
            node.Modulate = new Color(1f, 1f, 1f, 0.18f);
            cardNodes.Add(node);

            Tween gather = node.CreateTween().SetParallel();
            gather.TweenProperty(
                    node,
                    "global_position",
                    GetDaybreakCardPosition(center, index, visibleCount),
                    DaybreakGatherSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            gather.TweenProperty(
                    node,
                    "scale",
                    Vector2.One * 0.72f,
                    DaybreakGatherSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            gather.TweenProperty(
                node,
                "modulate",
                Colors.White,
                DaybreakGatherSeconds * 0.82f);
        }

        if (cardNodes.Count == 0)
            return;

        await Cmd.Wait(DaybreakGatherSeconds + DaybreakHoldSeconds);

        List<Task> exhaustAnimations = [];
        for (int index = 0; index < cardNodes.Count; index++)
        {
            NCard node = cardNodes[index];
            if (!GodotObject.IsInstanceValid(node))
                continue;

            if (index > 0)
                await Cmd.Wait(DaybreakExhaustStaggerSeconds);

            NCardExhaustVfx? exhaustVfx = NCardExhaustVfx.Create(node);
            if (exhaustVfx is null)
            {
                node.QueueFreeSafely();
                continue;
            }

            // Native Fiend Fire uses the ordinary 0.4-second dissolve. Only
            // these private preview instances are slowed down for readability.
            exhaustVfx._exhaustDuration = DaybreakExhaustSeconds;
            room.Ui.AddChildSafely(exhaustVfx);
            NDebugAudioManager.Instance?.Play("card_exhaust.mp3");
            exhaustAnimations.Add(exhaustVfx.PlayAnimation());
        }

        await Task.WhenAll(exhaustAnimations);
    }

    private static Vector2 GetDaybreakCardPosition(
        Vector2 center,
        int index,
        int count)
    {
        const int cardsPerRow = 8;
        int rowCount = (count + cardsPerRow - 1) / cardsPerRow;
        int row = index / cardsPerRow;
        int firstInRow = row * cardsPerRow;
        int countInRow = Math.Min(cardsPerRow, count - firstInRow);
        int column = index - firstInRow;
        float spacingX = countInRow <= 5 ? 154f : 118f;
        float x = (column - (countInRow - 1) * 0.5f) * spacingX;
        float y = (row - (rowCount - 1) * 0.5f) * 172f;
        return center + new Vector2(x, y) - NCard.defaultSize * 0.36f;
    }

    private static Vector2 GetDaybreakCardOrigin(
        CardModel card,
        NCard visualNode,
        Vector2 center,
        int index)
    {
        NCard? tableCard = NCard.FindOnTable(card);
        if (tableCard is not null)
            return tableCard.GlobalPosition;

        Vector2 source = card.Pile?.Type.GetTargetPosition(visualNode) ?? center;
        float spread = (index % 5 - 2) * 13f;
        return source + new Vector2(spread, 0f) - NCard.defaultSize * 0.11f;
    }

    /// <summary>
    /// Shows the blue-note anticipation for Eighty-Eight Keys once for every
    /// living enemy, then releases all bursts together. The only awaited time is
    /// the short shared anticipation, so multiple enemies do not serialize VFX.
    /// </summary>
    public static async Task PlayUniverseOf88Keys(
        IReadOnlyList<Creature> targets,
        int noteCount)
    {
        if (TestMode.IsOn ||
            NCombatRoom.Instance is null ||
            targets.Count == 0 ||
            noteCount <= 0)
        {
            return;
        }

        List<MgrUniverseNoteBurstVisual> visuals = [];
        foreach (Creature target in targets)
        {
            NCreature? creatureNode = NCombatRoom.Instance.GetCreatureNode(target);
            if (creatureNode is null)
                continue;

            var visual = new MgrUniverseNoteBurstVisual();
            visual.Initialize(noteCount);
            visual.GlobalPosition = creatureNode.VfxSpawnPosition + new Vector2(0f, -92f);
            NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(visual);
            visuals.Add(visual);
        }

        if (visuals.Count == 0)
            return;

        await Cmd.Wait(0.22f);
        foreach (MgrUniverseNoteBurstVisual visual in visuals)
        {
            if (GodotObject.IsInstanceValid(visual))
                visual.Burst();
        }
    }
}

public enum MgrAbilityVfxStyle
{
    Score,
    Echo,
    Wheel,
    Blood,
    Dual,
    Horizon,
    Seal,
    Prism,
    Satellite,
    Eye,
    Creation,
    Dawn,
    Glitch,
    Galaxy,
    Meteor,
    Cut,
    Ghost,
    Resonance,
    Electric,
    Mirage,
    Nocturne,
    Wind,
    Cloud,
    Siren,
    Neon
}

internal sealed partial class MgrAbilityBurstVisual : Node2D
{
    private static readonly Color[] Rainbow =
    [
        new("ff7e94"), new("ffd56a"), new("8ff0ce"),
        new("75cfff"), new("c697ff")
    ];

    private MgrAbilityVfxStyle _style;
    private float _scale = 1f;
    private float _age;
    private const float Lifetime = 0.62f;

    public void Initialize(MgrAbilityVfxStyle style, float scale)
    {
        _style = style;
        _scale = scale;
        ZIndex = 8;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = Math.Clamp(_age / Lifetime, 0f, 1f);
        float envelope = MathF.Sin(progress * MathF.PI);
        float radius = Mathf.Lerp(34f, 126f, EaseOut(progress)) * _scale;
        (Color primary, Color secondary) = GetPalette(_style);

        Color halo = primary;
        halo.A = 0.11f * envelope;
        DrawCircle(Vector2.Zero, radius * 0.82f, halo);

        Color ring = primary;
        ring.A = 0.72f * envelope;
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 52, ring, 2.4f * _scale, true);

        Color inner = secondary;
        inner.A = 0.48f * envelope;
        DrawArc(
            Vector2.Zero,
            radius * 0.68f,
            -progress * 2.1f,
            Mathf.Tau - progress * 2.1f,
            42,
            inner,
            1.4f * _scale,
            true);

        DrawStyleSymbol(radius, envelope, primary, secondary, progress);
        DrawShards(radius, envelope, progress);
    }

    private void DrawStyleSymbol(
        float radius,
        float envelope,
        Color primary,
        Color secondary,
        float progress)
    {
        Color color = secondary;
        color.A = 0.78f * envelope;
        float spin = progress * 1.5f;

        switch (_style)
        {
            case MgrAbilityVfxStyle.Echo:
                for (int index = 0; index < 3; index++)
                {
                    DrawArc(
                        new Vector2(index * radius * 0.08f, 0f),
                        radius * (0.24f + index * 0.12f),
                        -1.05f,
                        1.05f,
                        18,
                        color,
                        2.2f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Eye:
                DrawArc(Vector2.Zero, radius * 0.50f, 0.35f, MathF.PI - 0.35f, 20, color, 3f, true);
                DrawArc(Vector2.Zero, radius * 0.50f, MathF.PI + 0.35f, Mathf.Tau - 0.35f, 20, color, 3f, true);
                DrawCircle(Vector2.Zero, radius * 0.10f, color);
                break;
            case MgrAbilityVfxStyle.Wheel:
                for (int index = 0; index < 8; index++)
                {
                    float angle = spin + index * Mathf.Tau / 8f;
                    DrawLine(
                        Vector2.FromAngle(angle) * radius * 0.30f,
                        Vector2.FromAngle(angle) * radius * 0.62f,
                        color,
                        2.2f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Blood:
                DrawCircle(new Vector2(0f, radius * 0.11f), radius * 0.17f, color);
                DrawLine(
                    new Vector2(0f, -radius * 0.42f),
                    new Vector2(-radius * 0.16f, radius * 0.02f),
                    color,
                    3f,
                    true);
                DrawLine(
                    new Vector2(0f, -radius * 0.42f),
                    new Vector2(radius * 0.16f, radius * 0.02f),
                    color,
                    3f,
                    true);
                break;
            case MgrAbilityVfxStyle.Dual:
                DrawArc(new Vector2(-radius * 0.15f, 0f), radius * 0.31f, 0f, Mathf.Tau, 24, color, 2.5f, true);
                DrawArc(new Vector2(radius * 0.15f, 0f), radius * 0.31f, 0f, Mathf.Tau, 24, color, 2.5f, true);
                break;
            case MgrAbilityVfxStyle.Cut:
                DrawLine(new Vector2(-radius * 0.62f, radius * 0.32f), new Vector2(radius * 0.62f, -radius * 0.32f), color, 4f, true);
                break;
            case MgrAbilityVfxStyle.Horizon:
                for (int index = -1; index <= 1; index++)
                {
                    float y = index * radius * 0.16f;
                    DrawLine(
                        new Vector2(-radius * (0.52f - Math.Abs(index) * 0.10f), y),
                        new Vector2(radius * (0.52f - Math.Abs(index) * 0.10f), y),
                        color,
                        index == 0 ? 3f : 1.7f,
                        true);
                }
                DrawCircle(Vector2.Zero, radius * 0.10f, color);
                break;
            case MgrAbilityVfxStyle.Seal:
                DrawArc(Vector2.Zero, radius * 0.39f, 0f, Mathf.Tau, 30, color, 2.8f, true);
                for (int index = 0; index < 6; index++)
                {
                    float angle = spin + index * Mathf.Tau / 6f;
                    DrawLine(
                        Vector2.FromAngle(angle) * radius * 0.17f,
                        Vector2.FromAngle(angle) * radius * 0.48f,
                        color,
                        2.1f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Satellite:
            case MgrAbilityVfxStyle.Galaxy:
                DrawFourPointStar(Vector2.Zero, radius * 0.26f, color);
                DrawArc(Vector2.Zero, radius * 0.46f, -0.35f + spin, 2.75f + spin, 24, color, 1.8f, true);
                DrawCircle(Vector2.FromAngle(2.75f + spin) * radius * 0.46f, radius * 0.055f, color);
                break;
            case MgrAbilityVfxStyle.Meteor:
                DrawFourPointStar(new Vector2(radius * 0.18f, -radius * 0.13f), radius * 0.20f, color);
                DrawLine(
                    new Vector2(-radius * 0.58f, radius * 0.46f),
                    new Vector2(radius * 0.05f, -radius * 0.02f),
                    color,
                    4.2f,
                    true);
                DrawLine(
                    new Vector2(-radius * 0.44f, radius * 0.51f),
                    new Vector2(radius * 0.02f, radius * 0.16f),
                    color,
                    1.8f,
                    true);
                break;
            case MgrAbilityVfxStyle.Dawn:
                DrawArc(Vector2.Zero, radius * 0.23f, MathF.PI, Mathf.Tau, 18, color, 3f, true);
                for (int index = 0; index < 7; index++)
                {
                    float angle = MathF.PI + index * MathF.PI / 6f;
                    DrawLine(
                        Vector2.FromAngle(angle) * radius * 0.31f,
                        Vector2.FromAngle(angle) * radius * 0.55f,
                        color,
                        2f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Glitch:
                for (int index = -2; index <= 2; index++)
                {
                    float y = index * radius * 0.13f;
                    float offset = index % 2 == 0 ? radius * 0.12f : -radius * 0.12f;
                    DrawLine(
                        new Vector2(-radius * 0.38f + offset, y),
                        new Vector2(radius * 0.38f + offset, y),
                        color,
                        index == 0 ? 3.2f : 1.7f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Ghost:
                DrawCircle(new Vector2(0f, -radius * 0.10f), radius * 0.22f, color);
                for (int index = -1; index <= 1; index++)
                {
                    DrawLine(
                        new Vector2(index * radius * 0.13f, radius * 0.04f),
                        new Vector2(index * radius * 0.18f, radius * 0.36f),
                        color,
                        2.2f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Resonance:
                for (int index = 1; index <= 3; index++)
                    DrawArc(Vector2.Zero, radius * index * 0.15f, -0.82f, 0.82f, 18, color, 2.1f, true);
                break;
            case MgrAbilityVfxStyle.Electric:
                for (int index = -2; index <= 2; index++)
                {
                    float x = index * radius * 0.13f;
                    DrawLine(
                        new Vector2(x - radius * 0.09f, -radius * 0.37f),
                        new Vector2(x + radius * 0.05f, -radius * 0.08f),
                        color,
                        2.4f,
                        true);
                    DrawLine(
                        new Vector2(x + radius * 0.05f, -radius * 0.08f),
                        new Vector2(x - radius * 0.04f, radius * 0.08f),
                        color,
                        2.4f,
                        true);
                    DrawLine(
                        new Vector2(x - radius * 0.04f, radius * 0.08f),
                        new Vector2(x + radius * 0.10f, radius * 0.37f),
                        color,
                        2.4f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Mirage:
                for (int index = 0; index < 4; index++)
                {
                    float offset = (index - 1.5f) * radius * 0.12f;
                    DrawArc(
                        new Vector2(offset, 0f),
                        radius * (0.25f + index * 0.045f),
                        -1.12f,
                        1.12f,
                        20,
                        color,
                        1.8f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Nocturne:
                DrawArc(Vector2.Zero, radius * 0.38f, 0.55f, 5.72f, 30, color, 3f, true);
                DrawCircle(new Vector2(radius * 0.15f, -radius * 0.08f), radius * 0.27f, new Color(0.06f, 0.035f, 0.10f, color.A));
                DrawMusicNote(new Vector2(-radius * 0.05f, radius * 0.04f), radius * 0.22f, color);
                break;
            case MgrAbilityVfxStyle.Wind:
                for (int index = -2; index <= 2; index++)
                {
                    float y = index * radius * 0.14f;
                    float length = radius * (0.31f + (2 - Math.Abs(index)) * 0.08f);
                    DrawArc(
                        new Vector2(-radius * 0.12f, y),
                        length,
                        -0.42f,
                        0.42f,
                        18,
                        color,
                        1.8f,
                        true);
                }
                break;
            case MgrAbilityVfxStyle.Cloud:
                for (int index = -2; index <= 2; index++)
                {
                    DrawCircle(
                        new Vector2(index * radius * 0.13f, -MathF.Abs(index) * radius * 0.035f),
                        radius * (0.16f + (2 - Math.Abs(index)) * 0.025f),
                        new Color(color.R, color.G, color.B, color.A * 0.38f));
                }
                DrawLine(new Vector2(-radius * 0.12f, radius * 0.12f), new Vector2(radius * 0.02f, radius * 0.28f), color, 2.8f, true);
                DrawLine(new Vector2(radius * 0.02f, radius * 0.28f), new Vector2(-radius * 0.04f, radius * 0.44f), color, 2.8f, true);
                break;
            case MgrAbilityVfxStyle.Siren:
                for (int index = 1; index <= 4; index++)
                {
                    DrawArc(
                        new Vector2(-radius * 0.28f, 0f),
                        radius * index * 0.13f,
                        -0.78f,
                        0.78f,
                        18,
                        color,
                        2f,
                        true);
                }
                DrawMusicNote(new Vector2(-radius * 0.32f, 0f), radius * 0.22f, color);
                break;
            case MgrAbilityVfxStyle.Neon:
                for (int index = 0; index < 3; index++)
                {
                    Color neon = Rainbow[(index + 2) % Rainbow.Length];
                    neon.A = color.A;
                    float x = (index - 1) * radius * 0.22f;
                    DrawLine(new Vector2(x, -radius * 0.38f), new Vector2(x, radius * 0.38f), neon, 4f, true);
                    DrawCircle(new Vector2(x, -radius * 0.38f), radius * 0.055f, neon);
                    DrawCircle(new Vector2(x, radius * 0.38f), radius * 0.055f, neon);
                }
                break;
            case MgrAbilityVfxStyle.Prism:
            case MgrAbilityVfxStyle.Creation:
                for (int index = 0; index < Rainbow.Length; index++)
                {
                    Color rainbow = Rainbow[index];
                    rainbow.A = 0.78f * envelope;
                    float angle = spin + index * Mathf.Tau / Rainbow.Length;
                    DrawLine(
                        Vector2.FromAngle(angle) * radius * 0.26f,
                        Vector2.FromAngle(angle) * radius * 0.65f,
                        rainbow,
                        3.2f,
                        true);
                }
                break;
            default:
                DrawMusicNote(new Vector2(0f, radius * 0.05f), radius * 0.34f, color);
                break;
        }
    }

    private void DrawFourPointStar(Vector2 center, float radius, Color color)
    {
        DrawLine(center - Vector2.Right * radius, center + Vector2.Right * radius, color, 2.2f, true);
        DrawLine(center - Vector2.Up * radius, center + Vector2.Up * radius, color, 2.2f, true);
        Vector2 diagonal = new Vector2(0.48f, 0.48f) * radius;
        DrawLine(center - diagonal, center + diagonal, color, 1.2f, true);
        diagonal.X *= -1f;
        DrawLine(center - diagonal, center + diagonal, color, 1.2f, true);
    }

    private void DrawShards(float radius, float envelope, float progress)
    {
        for (int index = 0; index < 10; index++)
        {
            float angle = index * Mathf.Tau / 10f + progress * 0.42f;
            float distance = radius * Mathf.Lerp(0.72f, 1.12f, progress);
            Color color = Rainbow[index % Rainbow.Length];
            color.A = envelope * 0.68f;
            Vector2 center = Vector2.FromAngle(angle) * distance;
            float size = (index % 3 + 2f) * _scale;
            DrawLine(center - Vector2.Right * size, center + Vector2.Right * size, color, 1.5f, true);
            DrawLine(center - Vector2.Up * size, center + Vector2.Up * size, color, 1.5f, true);
        }
    }

    private void DrawMusicNote(Vector2 center, float size, Color color)
    {
        DrawSetTransform(center);
        DrawCircle(new Vector2(-size * 0.18f, size * 0.28f), size * 0.18f, color);
        DrawLine(
            new Vector2(-size * 0.02f, size * 0.28f),
            new Vector2(-size * 0.02f, -size * 0.52f),
            color,
            Math.Max(2f, size * 0.08f),
            true);
        DrawLine(
            new Vector2(-size * 0.02f, -size * 0.52f),
            new Vector2(size * 0.38f, -size * 0.36f),
            color,
            Math.Max(2f, size * 0.08f),
            true);
        DrawSetTransform(Vector2.Zero);
    }

    private static (Color Primary, Color Secondary) GetPalette(MgrAbilityVfxStyle style) =>
        style switch
        {
            MgrAbilityVfxStyle.Blood => (new Color("8d1830"), new Color("ff637f")),
            MgrAbilityVfxStyle.Ghost => (new Color("8a78b8"), new Color("e4ddff")),
            MgrAbilityVfxStyle.Dawn => (new Color("ff9c67"), new Color("fff3b0")),
            MgrAbilityVfxStyle.Glitch => (new Color("ee4a93"), new Color("56f3ea")),
            MgrAbilityVfxStyle.Galaxy => (new Color("6659d4"), new Color("a9eeff")),
            MgrAbilityVfxStyle.Meteor => (new Color("7658e8"), new Color("ffd58c")),
            MgrAbilityVfxStyle.Seal => (new Color("8c294b"), new Color("ff9fbd")),
            MgrAbilityVfxStyle.Horizon => (new Color("653282"), new Color("ffb2ed")),
            MgrAbilityVfxStyle.Satellite => (new Color("8066ef"), new Color("fff5af")),
            MgrAbilityVfxStyle.Electric => (new Color("4265d8"), new Color("9ff6ff")),
            MgrAbilityVfxStyle.Mirage => (new Color("6b77a8"), new Color("d6e9ff")),
            MgrAbilityVfxStyle.Nocturne => (new Color("3a183f"), new Color("d893dc")),
            MgrAbilityVfxStyle.Wind => (new Color("91c9dc"), new Color("f4ffff")),
            MgrAbilityVfxStyle.Cloud => (new Color("6676a8"), new Color("c7e8ff")),
            MgrAbilityVfxStyle.Siren => (new Color("5362ba"), new Color("c5a8ff")),
            MgrAbilityVfxStyle.Neon => (new Color("ea50cf"), new Color("70f4ff")),
            _ => (new Color("bd78ec"), new Color("bcecff"))
        };

    private static float EaseOut(float value) => 1f - MathF.Pow(1f - value, 3f);
}

internal sealed partial class MgrUniverseNoteBurstVisual : Node2D
{
    private readonly List<NoteGlyph> _notes = [];
    private float _age;
    private bool _bursting;
    private const float BurstLifetime = 0.48f;

    public void Initialize(int noteCount)
    {
        int count = Math.Max(1, noteCount);
        for (int index = 0; index < count; index++)
        {
            float centered = index - (count - 1) * 0.5f;
            float row = index % 2 == 0 ? 0f : -1f;
            _notes.Add(new NoteGlyph
            {
                Position = new Vector2(centered * 31f, row * 22f - MathF.Abs(centered) * 3f),
                Scale = 0.82f + index % 3 * 0.12f,
                Phase = index * 0.73f
            });
        }

        ZIndex = 12;
        SetProcess(true);
        QueueRedraw();
    }

    public void Burst()
    {
        _bursting = true;
        _age = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        // A combat transition can cancel the awaiting command that normally
        // calls Burst(). Fall back to an automatic release so the visual can
        // never remain attached to the combat canvas indefinitely.
        if (!_bursting && _age >= 0.52f)
        {
            Burst();
            return;
        }

        if (_bursting && _age >= BurstLifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_bursting)
        {
            float appear = Math.Clamp(_age / 0.18f, 0f, 1f);
            foreach (NoteGlyph note in _notes)
                DrawNote(note.Position, note.Scale * appear, 0.88f * appear, note.Phase);
            return;
        }

        float progress = Math.Clamp(_age / BurstLifetime, 0f, 1f);
        float alpha = 1f - progress;
        foreach (NoteGlyph note in _notes)
        {
            Vector2 direction = note.Position.LengthSquared() > 1f
                ? note.Position.Normalized()
                : Vector2.Up;
            Vector2 position = note.Position + direction * 54f * progress;
            float scale = note.Scale * Mathf.Lerp(1.1f, 2.05f, progress);
            DrawNote(position, scale, alpha, note.Phase);

            Color shard = new Color("91e8ff");
            shard.A = alpha * 0.78f;
            for (int index = 0; index < 3; index++)
            {
                float angle = note.Phase + index * Mathf.Tau / 3f;
                Vector2 start = position + Vector2.FromAngle(angle) * 12f * scale;
                Vector2 end = position + Vector2.FromAngle(angle) * (24f + 38f * progress) * scale;
                DrawLine(start, end, shard, 2f, true);
            }
        }
    }

    private void DrawNote(Vector2 center, float scale, float alpha, float phase)
    {
        Color halo = new Color("4b8fff");
        halo.A = alpha * 0.16f;
        DrawCircle(center, 22f * scale, halo);

        Color body = new Color("9deaff");
        body.A = alpha;
        Vector2 head = center + new Vector2(-5f, 7f) * scale;
        DrawCircle(head, 7.5f * scale, body);
        DrawLine(
            head + new Vector2(6f, 0f) * scale,
            head + new Vector2(6f, -30f) * scale,
            body,
            4f * scale,
            true);
        DrawLine(
            head + new Vector2(6f, -30f) * scale,
            head + new Vector2(18f, -23f + MathF.Sin(phase) * 2f) * scale,
            body,
            4f * scale,
            true);

        Color core = Colors.White;
        core.A = alpha * 0.72f;
        DrawCircle(head + new Vector2(-1f, -1f) * scale, 2.5f * scale, core);
    }

    private sealed class NoteGlyph
    {
        public Vector2 Position;
        public float Scale;
        public float Phase;
    }
}
