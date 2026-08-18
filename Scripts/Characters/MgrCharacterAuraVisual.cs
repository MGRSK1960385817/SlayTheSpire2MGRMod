using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MGRMod.Mechanics;
using MGRMod.Powers;
using MGRMod.Settings;

namespace MGRMod.Characters;

/// <summary>
/// Texture-free ambient presentation around MGR's combat character. Sparse
/// stars, rising light motes and expanding wavy resonance rings share the soft
/// gold/lavender/cyan language used by the Note rack and Performance staff.
/// This node is visual-only: it reads the existing combat Starry-Note counter
/// to determine density and owns an independent RNG, so it cannot affect combat
/// randomness or character animation state.
/// </summary>
public sealed partial class MgrCharacterAuraVisual : Node2D
{
    [Export(PropertyHint.Range, "4,40,1")]
    public int StarCount { get; set; } = 10;

    [Export(PropertyHint.Range, "0,8,1")]
    public int StarsPerStarryNote { get; set; } = 2;

    [Export(PropertyHint.Range, "10,160,1")]
    public int MaximumStarCount { get; set; } = 80;

    [Export(PropertyHint.Range, "0,32,1")]
    public int LightMoteCount { get; set; } = 11;

    [Export(PropertyHint.Range, "80,360,1")]
    public float HorizontalExtent { get; set; } = 216f;

    [Export(PropertyHint.Range, "80,320,1")]
    public float VerticalExtent { get; set; } = 164f;

    [Export]
    public Vector2 AuraCenter { get; set; } = new(0f, -170f);

    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float Intensity { get; set; } = 0.82f;

    [Export(PropertyHint.Range, "1,10,0.1")]
    public float ResonanceCycleSeconds { get; set; } = 4.8f;

    [Export(PropertyHint.Range, "0.3,1.2,0.01")]
    public float ResonanceInnerScale { get; set; } = 0.68f;

    [Export(PropertyHint.Range, "0.05,0.6,0.01")]
    public float ResonanceExpansion { get; set; } = 0.20f;

    [Export(PropertyHint.Range, "0,12,1")]
    public int ConstellationLinkCount { get; set; } = 2;

    [Export(PropertyHint.Range, "0,4,1")]
    public int LinksPerStarryNote { get; set; } = 1;

    [Export(PropertyHint.Range, "2,96,1")]
    public int MaximumConstellationLinkCount { get; set; } = 48;

    [Export(PropertyHint.Range, "60,220,1")]
    public float ConstellationLinkDistance { get; set; } = 122f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ConstellationLinkAffinity { get; set; } = 0.34f;

    [Export(PropertyHint.Range, "0,0.5,0.01")]
    public float ConstellationLinkAlpha { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0,0.2,0.005")]
    public float ResonanceWaveAmplitude { get; set; } = 0.048f;

    [Export(PropertyHint.Range, "3,12,1")]
    public int ResonanceWaveCount { get; set; } = 7;

    [Export]
    public Vector2 CanonWheelOffset { get; set; } = new(0f, 4f);

    [Export(PropertyHint.Range, "100,260,1")]
    public float CanonWheelRadius { get; set; } = 196f;

    [Export(PropertyHint.Range, "1,16,0.1")]
    public float CanonTriggerMinimumAngularSpeed { get; set; } = 3.2f;

    [Export(PropertyHint.Range, "1,20,0.1")]
    public float CanonTriggerMaximumAngularSpeed { get; set; } = 6.4f;

    [Export]
    public Vector2 HiganOrbitRadius { get; set; } = new(196f, 112f);

    [Export(PropertyHint.Range, "0.4,1.2,0.01")]
    public float CharacterAfterimageDistanceScale { get; set; } = 0.50f;

    [Export(PropertyHint.Range, "70,220,1")]
    public float PrismaticCrownRadius { get; set; } = 132f;

    private static readonly Color[] Palette =
    [
        new Color("fff1b8"), // warm stage light
        new Color("ffd0ef"), // rose
        new Color("d9c9ff"), // performance lavender
        new Color("bdeeff"), // staff sweep cyan
        new Color("d8ffd8"), // restrained note green
        new Color("ffbf8f")  // MGR theme orange
    ];

    private static readonly Color[] ResonancePalette =
    [
        new Color("d9c9ff"),
        new Color("bdeeff"),
        new Color("fff1b8")
    ];

    private readonly List<AmbientStar> _stars = [];
    private readonly List<LightMote> _motes = [];
    private readonly RandomNumberGenerator _random = new();
    private Player? _player;
    private double _elapsed;
    private float _satelliteVisibility;
    private Vector2 _satelliteOrbitRadius;
    private float _satelliteOrbitTilt;
    private float _satelliteAngularSpeed;
    private float _satellitePhaseOffset;
    private float _satelliteWobblePhase;
    private float _satelliteSpinSpeed;
    private float _satelliteSpinPhase;
    private CanonFormPower? _observedCanonPower;
    private int _lastCanonTriggerSerial;
    private float _canonVisibility;
    private float _canonRotation;
    private float _canonSpinRemaining;
    private float _canonFlash;
    private float _higanVisibility;
    private Sprite2D? _characterVisual;
    private readonly List<CharacterAfterimage> _characterAfterimages = [];
    private float _prismaticVisibility;
    private int _activeConstellationLinkCount;
    private int _lastStarryNoteCount = -1;

    public override void _Ready()
    {
        if (!MgrVisualSettings.ShouldLoadCharacterEffects)
        {
            Visible = false;
            SetProcess(false);
            return;
        }

        _random.Randomize();
        RandomizeSatelliteOrbit();
        TryResolvePlayer();
        ResolveCharacterVisual();
        RebuildParticles();
        RefreshDynamicDensity(force: true);
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;

        _elapsed += delta;
        float elapsed = (float)delta;
        RefreshDynamicDensity(force: false);
        float satelliteTarget = IsSatelliteAvailable() ? 1f : 0f;
        _satelliteVisibility = Mathf.MoveToward(
            _satelliteVisibility,
            satelliteTarget,
            elapsed * (satelliteTarget > 0f ? 4.5f : 7.5f));
        UpdateCanonWheel(elapsed);
        _higanVisibility = Mathf.MoveToward(
            _higanVisibility,
            HasDoubleNotes() ? 1f : 0f,
            elapsed * 3.4f);
        UpdateCharacterAfterimages(elapsed);
        _prismaticVisibility = Mathf.MoveToward(
            _prismaticVisibility,
            HasPrismaticPower() ? 1f : 0f,
            elapsed * 3.1f);

        foreach (AmbientStar star in _stars)
        {
            star.Age += elapsed;
            star.Position += star.Velocity * elapsed;
            if (star.Age >= star.Lifetime)
                RandomizeStar(star, startAtRandomAge: false);
        }

        foreach (LightMote mote in _motes)
        {
            mote.Age += elapsed;
            if (mote.Age >= mote.Lifetime)
                RandomizeMote(mote, startAtRandomAge: false);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float intensity = MathF.Max(0f, Intensity);
        DrawCanonWheel(intensity);
        DrawHiganDoubleOrbit(intensity);
        DrawPrismaticCrown(intensity);
        DrawResonanceCrescents(intensity);
        DrawConstellationLinks(intensity);
        DrawSatelliteStar(intensity);

        foreach (LightMote mote in _motes)
            DrawMote(mote, intensity);

        foreach (AmbientStar star in _stars)
            DrawStar(star, intensity);
    }

    private void RebuildParticles()
    {
        _stars.Clear();
        for (int index = 0; index < GetTargetStarCount(); index++)
        {
            var star = new AmbientStar();
            RandomizeStar(star, startAtRandomAge: true);
            _stars.Add(star);
        }

        _motes.Clear();
        for (int index = 0; index < Math.Max(0, LightMoteCount); index++)
        {
            var mote = new LightMote();
            RandomizeMote(mote, startAtRandomAge: true);
            _motes.Add(mote);
        }
    }

    private void RefreshDynamicDensity(bool force)
    {
        TryResolvePlayer();
        int starryNoteCount = GetStarryNoteCount();
        if (!force && starryNoteCount == _lastStarryNoteCount)
            return;

        _lastStarryNoteCount = starryNoteCount;
        int targetStarCount = GetTargetStarCount();
        while (_stars.Count < targetStarCount)
        {
            var star = new AmbientStar();
            // Newly earned stars fade into the field from age zero instead of
            // appearing at full brightness on the same frame as the Note.
            RandomizeStar(star, startAtRandomAge: false);
            _stars.Add(star);
        }

        if (_stars.Count > targetStarCount)
            _stars.RemoveRange(targetStarCount, _stars.Count - targetStarCount);

        _activeConstellationLinkCount = Math.Clamp(
            ConstellationLinkCount + starryNoteCount * LinksPerStarryNote,
            0,
            Math.Max(0, MaximumConstellationLinkCount));
    }

    private int GetTargetStarCount() => Math.Clamp(
        StarCount + GetStarryNoteCount() * StarsPerStarryNote,
        0,
        Math.Max(0, MaximumStarCount));

    private int GetStarryNoteCount()
    {
        return _player is not null &&
            MgrCombatStateStore.TryGet(_player, out MgrCombatState state)
                ? Math.Max(0, state.StarryNotesGeneratedThisCombat)
                : 0;
    }

    private void TryResolvePlayer()
    {
        if (_player is not null)
            return;

        Node? ancestor = GetParent();
        while (ancestor is not null)
        {
            if (ancestor is NCreature creature && creature.Entity?.Player is Player player)
            {
                _player = player;
                return;
            }

            ancestor = ancestor.GetParent();
        }

        // RitsuLib may place the custom scene behind an intermediate visuals
        // owner whose ancestry is not yet complete during _Ready. Keep the
        // ancestor route as the multiplayer-safe primary path, then recover
        // the local MGR player once the combat creature list is available.
        if (NCombatRoom.Instance is not { } room)
            return;

        Player? localPlayer = LocalContext.GetMe(
            room.CreatureNodes
                .Select(static node => node.Entity?.Player)
                .OfType<Player>());
        if (localPlayer?.Character is MgrCharacter)
            _player = localPlayer;
    }

    private bool IsSatelliteAvailable() =>
        _player?.Creature.Powers
            .OfType<SatelliteGirlPower>()
            .Any(static power => power.IsAvailableThisTurn) == true;

    private bool HasDoubleNotes() =>
        _player?.Creature.GetPowerAmount<DoubleNotesPower>() > 0m;

    private void ResolveCharacterVisual()
    {
        _characterVisual = GetParent()?.GetNodeOrNull<Sprite2D>("Visuals");
    }

    private void UpdateCharacterAfterimages(float delta)
    {
        if (_characterVisual is null)
            ResolveCharacterVisual();
        if (_characterVisual is null)
            return;

        HashSet<(AfterimageKind Kind, int Index)> desired = [];
        if (HasDoubleNotes())
            desired.Add((AfterimageKind.Higan, 0));
        if (_player?.Creature.GetPowerAmount<AttackNoteSilencePower>() > 0m)
            desired.Add((AfterimageKind.DualLovers, 0));

        int chaosCount = Math.Clamp(
            (int)(_player?.Creature.GetPowerAmount<ChaosMagicPower>() ?? 0m),
            0,
            8);
        for (int index = 0; index < chaosCount; index++)
            desired.Add((AfterimageKind.ChaosMagic, index));

        foreach ((AfterimageKind kind, int index) in desired)
        {
            if (_characterAfterimages.Any(state =>
                    state.Kind == kind && state.Index == index))
            {
                continue;
            }

            _characterAfterimages.Add(CreateCharacterAfterimage(kind, index));
        }

        for (int listIndex = _characterAfterimages.Count - 1;
             listIndex >= 0;
             listIndex--)
        {
            CharacterAfterimage state = _characterAfterimages[listIndex];
            bool active = desired.Contains((state.Kind, state.Index));
            state.Visibility = Mathf.MoveToward(
                state.Visibility,
                active ? 1f : 0f,
                delta * (active ? 3.1f : 4.8f));
            if (!active && state.Visibility <= 0.001f)
            {
                state.Sprite.QueueFree();
                _characterAfterimages.RemoveAt(listIndex);
                continue;
            }

            UpdateCharacterAfterimage(state);
        }
    }

    private CharacterAfterimage CreateCharacterAfterimage(
        AfterimageKind kind,
        int index)
    {
        ArgumentNullException.ThrowIfNull(_characterVisual);
        Vector2 baseOffset = kind switch
        {
            AfterimageKind.Higan => new Vector2(-35f, 24f),
            AfterimageKind.DualLovers => new Vector2(32f, 21f),
            _ => Vector2.FromAngle(index * 2.23f + _random.RandfRange(-0.4f, 0.4f)) *
                _random.RandfRange(24f, 43f) + Vector2.Down * 20f
        };
        baseOffset += new Vector2(
            _random.RandfRange(-5f, 5f),
            _random.RandfRange(-4f, 4f));
        baseOffset *= CharacterAfterimageDistanceScale;

        var sprite = new Sprite2D
        {
            Name = $"MgrAfterimage_{kind}_{index}",
            Centered = _characterVisual.Centered,
            TextureFilter = _characterVisual.TextureFilter,
            // Keep every persistent character visual at the default combat Z.
            // The scene-tree order (aura children before the later Visuals
            // sibling) places this copy behind MGR without allowing it to
            // overdraw card-selection and inspection UI at global Z 0.
            Visible = false
        };
        AddChild(sprite);
        return new CharacterAfterimage
        {
            Kind = kind,
            Index = index,
            Sprite = sprite,
            BaseOffset = baseOffset,
            PhaseX = _random.RandfRange(0f, Mathf.Tau),
            PhaseY = _random.RandfRange(0f, Mathf.Tau),
            SpeedX = _random.RandfRange(0.46f, 1.02f),
            SpeedY = _random.RandfRange(0.38f, 0.91f),
            DriftRadius = new Vector2(
                _random.RandfRange(5f, 14f),
                _random.RandfRange(4f, 11f)) *
                CharacterAfterimageDistanceScale,
            ScaleMultiplier = _random.RandfRange(0.95f, 1.01f),
            ColorPhase = _random.RandfRange(0f, 1f)
        };
    }

    private void UpdateCharacterAfterimage(CharacterAfterimage state)
    {
        ArgumentNullException.ThrowIfNull(_characterVisual);
        float time = (float)_elapsed;
        Vector2 drift = new(
            MathF.Sin(time * state.SpeedX + state.PhaseX) * state.DriftRadius.X,
            MathF.Cos(time * state.SpeedY + state.PhaseY) * state.DriftRadius.Y);

        // The animation state machine swaps the source Sprite2D texture every
        // frame. Every independently drifting copy mirrors that exact texture,
        // so multiple ability shadows stay frame-perfect while never sitting
        // at precisely the same offset.
        state.Sprite.Texture = _characterVisual.Texture;
        state.Sprite.Position = _characterVisual.Position + state.BaseOffset + drift;
        state.Sprite.Scale = _characterVisual.Scale * Vector2.One * state.ScaleMultiplier;
        state.Sprite.Rotation = _characterVisual.Rotation;
        state.Sprite.FlipH = _characterVisual.FlipH;
        state.Sprite.FlipV = _characterVisual.FlipV;
        state.Sprite.Modulate = GetAfterimageColor(state, time);
        state.Sprite.Visible = state.Visibility > 0.005f;
    }

    private static Color GetAfterimageColor(
        CharacterAfterimage state,
        float time) => state.Kind switch
    {
        AfterimageKind.Higan =>
            new Color(0.18f, 0.48f, 1f, state.Visibility * 0.52f),
        AfterimageKind.DualLovers =>
            new Color(1f, 0.22f, 0.30f, state.Visibility * 0.48f),
        _ => Color.FromHsv(
            Mathf.PosMod(state.ColorPhase + time * 0.085f, 1f),
            0.68f,
            1f,
            state.Visibility * 0.34f)
    };

    private enum AfterimageKind
    {
        Higan,
        DualLovers,
        ChaosMagic
    }

    private sealed class CharacterAfterimage
    {
        public required AfterimageKind Kind;
        public required int Index;
        public required Sprite2D Sprite;
        public required Vector2 BaseOffset;
        public required float PhaseX;
        public required float PhaseY;
        public required float SpeedX;
        public required float SpeedY;
        public required Vector2 DriftRadius;
        public required float ScaleMultiplier;
        public required float ColorPhase;
        public float Visibility;
    }

    private bool HasPrismaticPower() =>
        _player?.Creature.GetPowerAmount<PrismaticPower>() > 0m;

    private void UpdateCanonWheel(float delta)
    {
        CanonFormPower? power = _player?.Creature.GetPower<CanonFormPower>();
        _canonVisibility = Mathf.MoveToward(
            _canonVisibility,
            power is null ? 0f : 1f,
            delta * 3.2f);

        if (power is null)
        {
            _observedCanonPower = null;
            _lastCanonTriggerSerial = 0;
            _canonSpinRemaining = 0f;
            _canonFlash = Mathf.MoveToward(_canonFlash, 0f, delta * 2.8f);
            return;
        }

        if (!ReferenceEquals(power, _observedCanonPower))
        {
            _observedCanonPower = power;
            _lastCanonTriggerSerial = power.VisualTriggerSerial;
        }
        else if (power.VisualTriggerSerial > _lastCanonTriggerSerial)
        {
            int triggers = power.VisualTriggerSerial - _lastCanonTriggerSerial;
            _lastCanonTriggerSerial = power.VisualTriggerSerial;
            _canonSpinRemaining += Mathf.Tau * triggers;
            _canonFlash = 1f;
        }

        if (_canonSpinRemaining > 0f)
        {
            // Ease through exactly one complete revolution per trigger. Extra
            // queued triggers add another revolution instead of snapping.
            float speed = Mathf.Lerp(
                CanonTriggerMinimumAngularSpeed,
                CanonTriggerMaximumAngularSpeed,
                Math.Clamp(_canonSpinRemaining / Mathf.Tau, 0f, 1f));
            float step = MathF.Min(_canonSpinRemaining, speed * delta);
            _canonRotation = Mathf.PosMod(_canonRotation + step, Mathf.Tau);
            _canonSpinRemaining -= step;
        }

        _canonFlash = Mathf.MoveToward(_canonFlash, 0f, delta * 0.68f);
    }

    private void DrawCanonWheel(float intensity)
    {
        float alpha = _canonVisibility * intensity;
        if (alpha <= 0.005f)
            return;

        Vector2 center = AuraCenter + CanonWheelOffset;
        float pulse = 1f + _canonFlash * 0.16f;
        float outerRadius = CanonWheelRadius * pulse;
        float rotation = _canonRotation;

        float rainbowPhase = Mathf.PosMod((float)_elapsed * 0.055f, 1f);
        DrawRainbowArc(
            center,
            outerRadius * 1.01f,
            rotation,
            alpha * (0.10f + _canonFlash * 0.16f),
            18f + _canonFlash * 7f,
            rainbowPhase);
        DrawRainbowArc(
            center,
            outerRadius,
            rotation,
            alpha * (0.34f + _canonFlash * 0.55f),
            2.1f + _canonFlash * 3.2f,
            rainbowPhase);
        DrawRainbowArc(
            center,
            outerRadius * 0.72f,
            -rotation * 0.7f,
            alpha * (0.24f + _canonFlash * 0.38f),
            1.35f,
            rainbowPhase + 0.23f);
        DrawRainbowArc(
            center,
            outerRadius * 0.45f,
            rotation * 0.45f,
            alpha * (0.25f + _canonFlash * 0.32f),
            1.05f,
            rainbowPhase + 0.47f);

        // Sixty ticks form a clock face; the twelve major ticks read clearly
        // during the accelerated one-turn rotation.
        for (int index = 0; index < 60; index++)
        {
            float angle = rotation + index * Mathf.Tau / 60f;
            bool major = index % 5 == 0;
            float length = major ? 13f : 6f;
            Vector2 direction = Vector2.FromAngle(angle);
            Color tick = Color.FromHsv(
                Mathf.PosMod(rainbowPhase + index / 60f, 1f),
                major ? 0.58f : 0.46f,
                1f,
                alpha * (major ? 0.78f : 0.38f));
            DrawLine(
                center + direction * (outerRadius - length),
                center + direction * outerRadius,
                tick,
                major ? 2f : 1f,
                true);
        }

        // Eight visible moon phases rotate with the dial. Offset shadow discs
        // create changing crescents without relying on texture resources.
        for (int index = 0; index < 8; index++)
        {
            float angle = rotation + index * Mathf.Tau / 8f - MathF.PI * 0.5f;
            Vector2 moonCenter = center + Vector2.FromAngle(angle) * outerRadius * 0.83f;
            DrawMoonPhase(
                moonCenter,
                10.5f,
                index,
                alpha,
                rainbowPhase + index / 8f);
        }

        // A pair of time hands remains readable while the surrounding lunar
        // dial turns, reinforcing “last turn replayed this turn”.
        Color hand = Color.FromHsv(
            Mathf.PosMod(rainbowPhase + 0.12f, 1f),
            0.42f,
            1f,
            alpha * (0.52f + _canonFlash * 0.40f));
        DrawLine(center, center + Vector2.FromAngle(rotation * 0.35f - 1.2f) * 61f, hand, 2.4f, true);
        DrawLine(center, center + Vector2.FromAngle(-rotation * 0.22f + 0.35f) * 39f, hand, 3.2f, true);
        DrawCircle(center, 5.5f, hand);
    }

    private void DrawRainbowArc(
        Vector2 center,
        float radius,
        float rotation,
        float alpha,
        float width,
        float hueOffset)
    {
        const int segmentCount = 36;
        for (int index = 0; index < segmentCount; index++)
        {
            float start = rotation + index * Mathf.Tau / segmentCount;
            float end = rotation + (index + 1.08f) * Mathf.Tau / segmentCount;
            Color color = Color.FromHsv(
                Mathf.PosMod(hueOffset + index / (float)segmentCount, 1f),
                0.58f,
                1f,
                alpha);
            DrawArc(center, radius, start, end, 3, color, width, true);
        }
    }

    private void DrawMoonPhase(
        Vector2 center,
        float radius,
        int phase,
        float alpha,
        float hue)
    {
        Color shadow = new Color("261b36");
        shadow.A = alpha * 0.82f;
        DrawCircle(center, radius + 1.7f, shadow);

        float fullness = 1f - MathF.Abs(phase - 4f) / 4f;
        Color light = new Color("f6e8c7");
        light.A = alpha * Mathf.Lerp(0.22f, 0.85f, fullness);
        float lightRadius = Mathf.Lerp(radius * 0.34f, radius, fullness);
        float direction = phase < 4 ? -1f : 1f;
        Vector2 lightCenter = center + Vector2.Right * direction * (radius - lightRadius) * 0.68f;
        DrawCircle(lightCenter, lightRadius, light);

        Color rim = Color.FromHsv(
            Mathf.PosMod(hue, 1f),
            0.52f,
            1f,
            alpha * 0.42f);
        DrawArc(center, radius, 0f, Mathf.Tau, 20, rim, 1f, true);
    }

    private void DrawHiganDoubleOrbit(float intensity)
    {
        float alpha = _higanVisibility * intensity;
        if (alpha <= 0.005f)
            return;

        Vector2 center = AuraCenter + new Vector2(0f, 10f);
        float phase = (float)_elapsed * 0.72f;
        Color first = new Color("ff9bd5");
        Color second = new Color("8cecff");
        first.A = alpha * 0.26f;
        second.A = alpha * 0.26f;
        DrawEllipseArc(
            center,
            HiganOrbitRadius.X,
            HiganOrbitRadius.Y,
            -0.68f,
            0.68f,
            first,
            1.55f);
        DrawEllipseArc(
            center,
            HiganOrbitRadius.X,
            HiganOrbitRadius.Y,
            MathF.PI - 0.68f,
            MathF.PI + 0.68f,
            second,
            1.55f);

        for (int track = 0; track < 2; track++)
        {
            float angle = phase + track * MathF.PI;
            Vector2 position = center + new Vector2(
                MathF.Cos(angle) * HiganOrbitRadius.X,
                MathF.Sin(angle) * HiganOrbitRadius.Y);
            Color color = track == 0 ? first : second;
            color.A = alpha * 0.82f;
            DrawCircle(position, 5.2f, color);
            DrawLine(position, position + Vector2.Up * 17f, color, 2.2f, true);
            Color halo = color;
            halo.A *= 0.18f;
            DrawCircle(position, 18f, halo);
        }
    }

    private void DrawPrismaticCrown(float intensity)
    {
        float alpha = _prismaticVisibility * intensity;
        if (alpha <= 0.005f)
            return;

        Color[] colors =
        [
            new Color("ff799c"), new Color("ffd875"), new Color("98f1c5"),
            new Color("83ddff"), new Color("b8a0ff"), new Color("f59dff")
        ];
        Vector2 center = AuraCenter + new Vector2(0f, -8f);
        float rotation = (float)_elapsed * 0.16f;
        for (int index = 0; index < colors.Length; index++)
        {
            float angle = rotation + index * Mathf.Tau / colors.Length;
            Vector2 position = center + Vector2.FromAngle(angle) * PrismaticCrownRadius;
            Vector2 tangent = Vector2.FromAngle(angle + MathF.PI * 0.5f);
            Vector2 radial = Vector2.FromAngle(angle);
            Color color = colors[index];
            color.A = alpha * 0.32f;
            DrawLine(position - radial * 12f, position + radial * 12f, color, 3.2f, true);
            DrawLine(position - tangent * 5f, position + tangent * 5f, color, 2f, true);
        }
    }

    private void DrawEllipseArc(
        Vector2 center,
        float radiusX,
        float radiusY,
        float startAngle,
        float endAngle,
        Color color,
        float width)
    {
        const int segmentCount = 32;
        Vector2 previous = center + new Vector2(
            MathF.Cos(startAngle) * radiusX,
            MathF.Sin(startAngle) * radiusY);
        for (int index = 1; index <= segmentCount; index++)
        {
            float angle = Mathf.Lerp(
                startAngle,
                endAngle,
                index / (float)segmentCount);
            Vector2 current = center + new Vector2(
                MathF.Cos(angle) * radiusX,
                MathF.Sin(angle) * radiusY);
            DrawLine(previous, current, color, width, true);
            previous = current;
        }
    }

    private void DrawSatelliteStar(float intensity)
    {
        if (_satelliteVisibility <= 0.005f)
            return;

        float time = (float)_elapsed;
        float phase = time * _satelliteAngularSpeed + _satellitePhaseOffset;
        Vector2 position = GetSatelliteOrbitPosition(phase, time);
        float depth = 0.5f + 0.5f * MathF.Sin(phase);
        float pulse = 0.94f + 0.06f * MathF.Sin(time * 4.7f + _satelliteWobblePhase);
        float size = Mathf.Lerp(13.5f, 19.5f, depth) * pulse;
        float alpha = _satelliteVisibility * intensity *
            Mathf.Lerp(0.72f, 1f, depth);
        float spin = time * _satelliteSpinSpeed + _satelliteSpinPhase;

        // A short dotted trail makes the star read as an orbiting satellite
        // rather than another ambient twinkle.
        for (int index = 5; index >= 1; index--)
        {
            float trailPhase = phase - index * 0.13f;
            Vector2 trailPosition = GetSatelliteOrbitPosition(
                trailPhase,
                time - index * 0.045f);
            Color trail = new Color("b797ff");
            trail.A = alpha * (0.24f - index * 0.028f);
            DrawCircle(
                trailPosition,
                MathF.Max(1.8f, size * (0.22f - index * 0.019f)),
                trail);
        }

        Color halo = new Color("8a5cf2");
        halo.A = alpha * 0.15f;
        DrawCircle(position, size * 3.8f, halo);

        Color secondaryHalo = new Color("c7acff");
        secondaryHalo.A = alpha * 0.18f;
        DrawCircle(position, size * 2.35f, secondaryHalo);

        Color ray = new Color("eadfff");
        ray.A = alpha;
        Vector2 horizontal = Vector2.Right.Rotated(spin) * size * 1.75f;
        Vector2 vertical = Vector2.Up.Rotated(spin) * size * 1.75f;
        DrawLine(position - horizontal, position + horizontal, ray, 3.1f, true);
        DrawLine(position - vertical, position + vertical, ray, 3.1f, true);
        Vector2 diagonal = new Vector2(0.72f, 0.72f).Rotated(spin) * size;
        DrawLine(position - diagonal, position + diagonal, ray, 1.8f, true);
        diagonal = new Vector2(-0.72f, 0.72f).Rotated(spin) * size;
        DrawLine(position - diagonal, position + diagonal, ray, 1.8f, true);

        Color core = new Color("fbf7ff");
        core.A = alpha;
        DrawCircle(position, size * 0.34f, core);
    }

    private void RandomizeSatelliteOrbit()
    {
        _satelliteOrbitRadius = new Vector2(
            _random.RandfRange(170f, 216f),
            _random.RandfRange(98f, 142f));
        _satelliteOrbitTilt = _random.RandfRange(-0.42f, 0.42f);
        _satelliteAngularSpeed = _random.RandfRange(1.9f, 2.45f);
        _satellitePhaseOffset = _random.RandfRange(0f, Mathf.Tau);
        _satelliteWobblePhase = _random.RandfRange(0f, Mathf.Tau);
        _satelliteSpinSpeed = _random.RandfRange(0.85f, 1.25f) *
            (_random.Randf() < 0.5f ? -1f : 1f);
        _satelliteSpinPhase = _random.RandfRange(0f, Mathf.Tau);
    }

    private Vector2 GetSatelliteOrbitPosition(float phase, float time)
    {
        // The radius and tilt breathe at deliberately different low
        // frequencies. The star still clearly orbits MGR, but no two combat
        // instances trace the same mechanical ellipse.
        float radiusX = _satelliteOrbitRadius.X *
            (1f + 0.10f * MathF.Sin(time * 0.53f + _satelliteWobblePhase));
        float radiusY = _satelliteOrbitRadius.Y *
            (1f + 0.14f * MathF.Cos(time * 0.41f + _satelliteWobblePhase * 0.73f));
        float tilt = _satelliteOrbitTilt +
            0.11f * MathF.Sin(time * 0.34f + _satelliteWobblePhase);
        Vector2 local = new(
            MathF.Cos(phase) * radiusX,
            MathF.Sin(phase) * radiusY);
        return AuraCenter + local.Rotated(tilt);
    }

    private void RandomizeStar(AmbientStar star, bool startAtRandomAge)
    {
        // Most stars live beside the silhouette; a smaller group forms a loose
        // crown above it. This keeps the character readable while still making
        // the aura feel spatial rather than rectangular.
        if (_random.Randf() < 0.76f)
        {
            float side = _random.Randf() < 0.5f ? -1f : 1f;
            star.Position = AuraCenter + new Vector2(
                side * _random.RandfRange(
                    HorizontalExtent * 0.68f,
                    HorizontalExtent * 1.06f),
                _random.RandfRange(-VerticalExtent, VerticalExtent * 0.92f));
        }
        else
        {
            star.Position = AuraCenter + new Vector2(
                _random.RandfRange(
                    -HorizontalExtent * 0.76f,
                    HorizontalExtent * 0.76f),
                _random.RandfRange(
                    -VerticalExtent * 1.16f,
                    -VerticalExtent * 0.84f));
        }

        star.Lifetime = _random.RandfRange(2.4f, 5.8f);
        star.Age = startAtRandomAge
            ? _random.RandfRange(0f, star.Lifetime)
            : 0f;
        star.Velocity = new Vector2(
            _random.RandfRange(-4.5f, 4.5f),
            _random.RandfRange(-10.5f, -3f));
        star.Size = _random.RandfRange(1.7f, 4.4f);
        star.TwinkleRate = _random.RandfRange(2.2f, 5.6f);
        star.Phase = _random.RandfRange(0f, Mathf.Tau);
        star.Color = RandomPaletteColor();
        star.HasDiagonalRays = _random.Randf() < 0.24f;
        star.LinkAffinity = _random.Randf();
    }

    private void RandomizeMote(LightMote mote, bool startAtRandomAge)
    {
        mote.StartPosition = AuraCenter + new Vector2(
            _random.RandfRange(
                -HorizontalExtent * 0.82f,
                HorizontalExtent * 0.82f),
            _random.RandfRange(
                VerticalExtent * 0.72f,
                VerticalExtent * 1.10f));
        mote.Lifetime = _random.RandfRange(3.8f, 7.2f);
        mote.Age = startAtRandomAge
            ? _random.RandfRange(0f, mote.Lifetime)
            : 0f;
        mote.RiseDistance = _random.RandfRange(134f, 295f);
        mote.SwayAmplitude = _random.RandfRange(7.5f, 26f);
        mote.SwayCycles = _random.RandfRange(0.55f, 1.25f);
        mote.Phase = _random.RandfRange(0f, Mathf.Tau);
        mote.Size = _random.RandfRange(0.9f, 2.2f);
        mote.Color = RandomPaletteColor();
    }

    private void DrawResonanceCrescents(float intensity)
    {
        float cycle = MathF.Max(0.1f, ResonanceCycleSeconds);
        for (int index = 0; index < ResonancePalette.Length; index++)
        {
            float progress = Mathf.PosMod(
                (float)_elapsed / cycle + index / (float)ResonancePalette.Length,
                1f);
            float envelope = MathF.Pow(MathF.Sin(progress * MathF.PI), 2f);
            float alpha = envelope * 0.105f * intensity;
            if (alpha < 0.004f)
                continue;

            float radialScale = ResonanceInnerScale +
                progress * ResonanceExpansion;
            float radiusX = HorizontalExtent * radialScale;
            float radiusY = VerticalExtent * radialScale * 0.86f;
            Color color = ResonancePalette[index];
            color.A = alpha;
            Color glow = color;
            glow.A *= 0.34f;

            float wavePhase =
                (float)_elapsed * 0.42f + index * MathF.Tau / 3f;
            DrawWavyRing(
                AuraCenter,
                radiusX,
                radiusY,
                glow,
                5.4f,
                wavePhase);
            DrawWavyRing(
                AuraCenter,
                radiusX,
                radiusY,
                color,
                1.15f,
                wavePhase);
        }
    }

    private void DrawConstellationLinks(float intensity)
    {
        int linksDrawn = 0;
        for (int firstIndex = 0;
             firstIndex < _stars.Count && linksDrawn < _activeConstellationLinkCount;
             firstIndex++)
        {
            AmbientStar first = _stars[firstIndex];
            float firstAlpha = GetStarAlpha(first);
            if (first.LinkAffinity > ConstellationLinkAffinity || firstAlpha < 0.34f)
                continue;

            AmbientStar? nearest = null;
            float nearestDistance = ConstellationLinkDistance;
            for (int secondIndex = firstIndex + 1;
                 secondIndex < _stars.Count;
                 secondIndex++)
            {
                AmbientStar candidate = _stars[secondIndex];
                float distance = first.Position.DistanceTo(candidate.Position);
                if (distance >= nearestDistance || GetStarAlpha(candidate) < 0.30f)
                    continue;

                nearestDistance = distance;
                nearest = candidate;
            }

            if (nearest is null)
                continue;

            Color lineColor = first.Color.Lerp(nearest.Color, 0.5f);
            lineColor.A = MathF.Min(firstAlpha, GetStarAlpha(nearest)) *
                ConstellationLinkAlpha * intensity;
            DrawLine(
                first.Position,
                nearest.Position,
                lineColor,
                1f,
                antialiased: true);
            linksDrawn++;
        }
    }

    private void DrawMote(LightMote mote, float intensity)
    {
        float progress = Math.Clamp(mote.Age / mote.Lifetime, 0f, 1f);
        float envelope = MathF.Pow(MathF.Sin(progress * MathF.PI), 1.35f);
        float sway = MathF.Sin(
            mote.Phase + progress * MathF.Tau * mote.SwayCycles) *
            mote.SwayAmplitude;
        Vector2 position = mote.StartPosition + new Vector2(
            sway,
            -mote.RiseDistance * progress);
        float alpha = envelope * 0.42f * intensity;

        Color halo = mote.Color;
        halo.A = alpha * 0.14f;
        DrawCircle(position, mote.Size * 4.2f, halo);

        Color tail = mote.Color;
        tail.A = alpha * 0.22f;
        DrawLine(
            position + Vector2.Down * mote.Size * 1.4f,
            position + Vector2.Down * mote.Size * 6.2f,
            tail,
            MathF.Max(0.7f, mote.Size * 0.48f),
            antialiased: true);

        Color core = mote.Color;
        core.A = alpha;
        DrawCircle(position, mote.Size, core);
    }

    private void DrawStar(AmbientStar star, float intensity)
    {
        float alpha = GetStarAlpha(star) * intensity;
        if (alpha < 0.01f)
            return;

        float twinkle = 0.5f + 0.5f * MathF.Sin(
            (float)_elapsed * star.TwinkleRate + star.Phase);
        float size = star.Size * Mathf.Lerp(0.82f, 1.18f, twinkle);

        Color halo = star.Color;
        halo.A = alpha * 0.10f;
        DrawCircle(star.Position, size * 4f, halo);

        Color ray = star.Color;
        ray.A = alpha * 0.56f;
        DrawLine(
            star.Position - Vector2.Up * size * 2f,
            star.Position + Vector2.Up * size * 2f,
            ray,
            MathF.Max(0.8f, size * 0.25f),
            antialiased: true);
        DrawLine(
            star.Position - Vector2.Right * size * 1.35f,
            star.Position + Vector2.Right * size * 1.35f,
            ray,
            MathF.Max(0.8f, size * 0.22f),
            antialiased: true);

        if (star.HasDiagonalRays)
        {
            Vector2 diagonal = new(0.78f, 0.78f);
            diagonal *= size;
            Color diagonalColor = ray;
            diagonalColor.A *= 0.46f;
            DrawLine(
                star.Position - diagonal,
                star.Position + diagonal,
                diagonalColor,
                0.8f,
                antialiased: true);
            diagonal.X *= -1f;
            DrawLine(
                star.Position - diagonal,
                star.Position + diagonal,
                diagonalColor,
                0.8f,
                antialiased: true);
        }

        Color core = star.Color;
        core.A = alpha;
        DrawCircle(star.Position, MathF.Max(0.9f, size * 0.30f), core);
    }

    private float GetStarAlpha(AmbientStar star)
    {
        float progress = Math.Clamp(star.Age / star.Lifetime, 0f, 1f);
        float lifetimeEnvelope = MathF.Pow(
            MathF.Sin(progress * MathF.PI),
            1.25f);
        float twinkle = 0.66f + 0.34f * MathF.Sin(
            (float)_elapsed * star.TwinkleRate + star.Phase);
        return lifetimeEnvelope * Mathf.Lerp(0.46f, 0.86f, twinkle);
    }

    private void DrawWavyRing(
        Vector2 center,
        float radiusX,
        float radiusY,
        Color color,
        float width,
        float wavePhase)
    {
        const int segmentCount = 64;
        int waveCount = Math.Max(3, ResonanceWaveCount);
        float amplitude = MathF.Max(0f, ResonanceWaveAmplitude);
        Vector2 previous = GetWavyRingPoint(
            center,
            radiusX,
            radiusY,
            0f,
            waveCount,
            amplitude,
            wavePhase);
        for (int index = 1; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            float angle = t * MathF.Tau;
            Vector2 current = GetWavyRingPoint(
                center,
                radiusX,
                radiusY,
                angle,
                waveCount,
                amplitude,
                wavePhase);
            DrawLine(previous, current, color, width, antialiased: true);
            previous = current;
        }
    }

    private static Vector2 GetWavyRingPoint(
        Vector2 center,
        float radiusX,
        float radiusY,
        float angle,
        int waveCount,
        float amplitude,
        float phase)
    {
        float ripple =
            MathF.Sin(angle * waveCount - phase) +
            MathF.Sin(angle * Math.Max(2, waveCount - 3) + phase * 0.72f) *
                0.38f;
        float radialWave = 1f + ripple * amplitude;
        return center + new Vector2(
            MathF.Cos(angle) * radiusX * radialWave,
            MathF.Sin(angle) * radiusY * radialWave);
    }

    private Color RandomPaletteColor() =>
        Palette[_random.RandiRange(0, Palette.Length - 1)];

    private sealed class AmbientStar
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Size;
        public float TwinkleRate;
        public float Phase;
        public float LinkAffinity;
        public Color Color;
        public bool HasDiagonalRays;
    }

    private sealed class LightMote
    {
        public Vector2 StartPosition;
        public float Age;
        public float Lifetime;
        public float RiseDistance;
        public float SwayAmplitude;
        public float SwayCycles;
        public float Phase;
        public float Size;
        public Color Color;
    }
}
