using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SlayTheSpire2MGRMod.Mechanics;

namespace SlayTheSpire2MGRMod.Characters;

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
    private int _activeConstellationLinkCount;
    private int _lastStarryNoteCount = -1;

    public override void _Ready()
    {
        _random.Randomize();
        TryResolvePlayer();
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
        DrawResonanceCrescents(intensity);
        DrawConstellationLinks(intensity);

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
