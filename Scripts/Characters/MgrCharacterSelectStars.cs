using Godot;

namespace MGRMod.Characters;

/// <summary>
/// Procedural character-select ambience. The background is split into distant
/// dust, broad light ribbons, large connected constellations and a few meteors
/// so the screen has depth without depending on borrowed texture assets.
/// </summary>
public sealed partial class MgrCharacterSelectStars : Control
{
    [Export(PropertyHint.Range, "8,96,1")]
    public int StarCount { get; set; } = 34;

    [Export(PropertyHint.Range, "0,80,0.5")]
    public float BaseDriftSpeed { get; set; } = 10f;

    [Export(PropertyHint.Range, "0.1,4,0.05")]
    public float TwinkleSpeed { get; set; } = 1.15f;

    [Export(PropertyHint.Range, "0.5,2.5,0.05")]
    public float ConstellationScale { get; set; } = 1.22f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ConstellationLinkAlpha { get; set; } = 0.44f;

    [Export(PropertyHint.Range, "0,8,1")]
    public int MeteorCount { get; set; } = 4;

    [Export(PropertyHint.Range, "0,0.3,0.01")]
    public float RibbonAlpha { get; set; } = 0.08f;

    private static readonly Color[] Palette =
    [
        new Color("fff4c7"),
        new Color("ffd4f6"),
        new Color("d8e5ff"),
        new Color("e7d5ff"),
        new Color("bff7ff"),
        new Color("bba8ff")
    ];

    // These are deliberately hand-shaped instead of random point clouds. Their
    // silhouettes stay readable as constellations while the whole figure drifts.
    private static readonly Vector2[][] ConstellationPatterns =
    [
        [
            new(-0.52f, 0.02f), new(-0.31f, -0.34f), new(-0.06f, -0.18f),
            new(0.22f, -0.43f), new(0.49f, -0.08f), new(0.24f, 0.27f),
            new(-0.08f, 0.43f), new(-0.39f, 0.28f)
        ],
        [
            new(-0.55f, 0.20f), new(-0.32f, -0.16f), new(-0.05f, 0.05f),
            new(0.13f, -0.40f), new(0.30f, -0.04f), new(0.56f, -0.25f),
            new(0.43f, 0.28f), new(0.07f, 0.42f)
        ],
        [
            new(-0.50f, -0.22f), new(-0.18f, -0.38f), new(0.03f, -0.05f),
            new(0.34f, -0.30f), new(0.52f, 0.06f), new(0.20f, 0.37f),
            new(-0.12f, 0.19f), new(-0.42f, 0.40f)
        ],
        [
            new(-0.47f, 0.28f), new(-0.30f, -0.16f), new(-0.02f, -0.39f),
            new(0.23f, -0.09f), new(0.49f, -0.28f), new(0.42f, 0.24f),
            new(0.06f, 0.39f), new(-0.19f, 0.10f)
        ]
    ];

    private static readonly Link[][] ConstellationLinks =
    [
        [new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 5), new(5, 6), new(6, 7), new(7, 0), new(2, 6)],
        [new(0, 1), new(1, 2), new(2, 3), new(2, 4), new(4, 5), new(4, 6), new(6, 7), new(7, 2)],
        [new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 5), new(5, 6), new(6, 7), new(7, 0), new(2, 6)],
        [new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 5), new(5, 6), new(6, 7), new(7, 1), new(3, 7)]
    ];

    private static readonly Vector2[] ConstellationCenters =
    [
        new(0.15f, 0.20f),
        new(0.79f, 0.17f),
        new(0.84f, 0.67f),
        new(0.19f, 0.73f)
    ];

    private readonly List<AmbientStar> _stars = [];
    private readonly List<Constellation> _constellations = [];
    private readonly List<Meteor> _meteors = [];
    private readonly RandomNumberGenerator _random = new();
    private double _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        _random.Randomize();
        RebuildVisuals();
        Resized += RebuildVisuals;
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        Resized -= RebuildVisuals;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        Vector2 extent = GetUsableSize();
        float dt = (float)delta;

        foreach (AmbientStar star in _stars)
        {
            star.Position += star.Velocity * dt;
            if (star.Position.X > extent.X + 50f || star.Position.Y < -50f)
                ResetStarAtEdge(star, extent);
        }

        foreach (Meteor meteor in _meteors)
        {
            meteor.Age += dt;
            meteor.Position += meteor.Velocity * dt;
            if (meteor.Age >= meteor.Lifetime ||
                meteor.Position.X > extent.X + meteor.TailLength ||
                meteor.Position.Y < -meteor.TailLength)
            {
                ResetMeteor(meteor, extent, startVisible: false);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 extent = GetUsableSize();
        DrawLightRibbons(extent);
        DrawConstellations(extent);
        DrawMeteors();

        foreach (AmbientStar star in _stars)
            DrawAmbientStar(star);
    }

    private void RebuildVisuals()
    {
        Vector2 extent = GetUsableSize();

        _stars.Clear();
        for (int index = 0; index < StarCount; index++)
        {
            var star = new AmbientStar();
            RandomizeStar(star);
            star.Position = new Vector2(
                _random.RandfRange(0f, extent.X),
                _random.RandfRange(0f, extent.Y));
            _stars.Add(star);
        }

        _constellations.Clear();
        for (int index = 0; index < ConstellationPatterns.Length; index++)
        {
            var constellation = new Constellation
            {
                NormalizedCenter = ConstellationCenters[index],
                Pattern = ConstellationPatterns[index],
                Links = ConstellationLinks[index],
                Width = _random.RandfRange(245f, 340f),
                Height = _random.RandfRange(155f, 230f),
                DriftRadius = new Vector2(
                    _random.RandfRange(12f, 28f),
                    _random.RandfRange(8f, 20f)),
                DriftSpeed = _random.RandfRange(0.10f, 0.21f),
                Phase = _random.RandfRange(0f, Mathf.Tau),
                Rotation = _random.RandfRange(-0.13f, 0.13f),
                Color = Palette[_random.RandiRange(0, Palette.Length - 1)],
                TraceSpeed = _random.RandfRange(0.22f, 0.38f)
            };

            foreach (Vector2 unused in constellation.Pattern)
            {
                constellation.NodeSizes.Add(_random.RandfRange(5.0f, 8.8f));
                constellation.NodePhases.Add(_random.RandfRange(0f, Mathf.Tau));
            }

            _constellations.Add(constellation);
        }

        _meteors.Clear();
        for (int index = 0; index < MeteorCount; index++)
        {
            var meteor = new Meteor();
            ResetMeteor(meteor, extent, startVisible: true);
            _meteors.Add(meteor);
        }
    }

    private void DrawLightRibbons(Vector2 extent)
    {
        const int segments = 44;
        for (int ribbonIndex = 0; ribbonIndex < 3; ribbonIndex++)
        {
            var points = new Vector2[segments];
            float phase = (float)_elapsed * (0.055f + ribbonIndex * 0.018f) +
                ribbonIndex * 2.1f;
            float baseY = extent.Y * (0.25f + ribbonIndex * 0.235f);
            float amplitude = extent.Y * (0.032f + ribbonIndex * 0.007f);

            for (int index = 0; index < segments; index++)
            {
                float progress = index / (float)(segments - 1);
                float y = baseY +
                    MathF.Sin(progress * MathF.Tau * 1.18f + phase) * amplitude +
                    MathF.Sin(progress * MathF.Tau * 2.3f - phase * 0.7f) * amplitude * 0.28f;
                points[index] = new Vector2(progress * extent.X, y);
            }

            Color color = Palette[(ribbonIndex + 2) % Palette.Length];
            Color wideGlow = color;
            wideGlow.A = RibbonAlpha * 0.22f;
            DrawPolyline(points, wideGlow, 20f + ribbonIndex * 4f, true);

            Color core = color;
            core.A = RibbonAlpha;
            DrawPolyline(points, core, 2.2f + ribbonIndex * 0.35f, true);
        }
    }

    private void DrawConstellations(Vector2 extent)
    {
        foreach (Constellation constellation in _constellations)
        {
            Vector2 center = constellation.NormalizedCenter * extent + new Vector2(
                MathF.Sin((float)_elapsed * constellation.DriftSpeed + constellation.Phase) *
                    constellation.DriftRadius.X,
                MathF.Cos((float)_elapsed * constellation.DriftSpeed * 0.83f + constellation.Phase) *
                    constellation.DriftRadius.Y);
            float breathe = 1f + MathF.Sin(
                (float)_elapsed * 0.48f + constellation.Phase) * 0.035f;
            float rotation = constellation.Rotation + MathF.Sin(
                (float)_elapsed * 0.09f + constellation.Phase) * 0.035f;

            var positions = new Vector2[constellation.Pattern.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                Vector2 local = new Vector2(
                    constellation.Pattern[index].X * constellation.Width,
                    constellation.Pattern[index].Y * constellation.Height) *
                    ConstellationScale * breathe;
                positions[index] = center + local.Rotated(rotation);
            }

            float constellationPulse = 0.76f + 0.24f * MathF.Sin(
                (float)_elapsed * 0.72f + constellation.Phase);
            foreach (Link link in constellation.Links)
                DrawConstellationLink(positions[link.From], positions[link.To],
                    constellation.Color, constellationPulse);

            DrawTravelingConstellationPulse(constellation, positions);

            for (int index = 0; index < positions.Length; index++)
            {
                float twinkle = 0.74f + 0.26f * MathF.Sin(
                    (float)_elapsed * (1.05f + index * 0.07f) +
                    constellation.NodePhases[index]);
                DrawConstellationStar(
                    positions[index],
                    constellation.NodeSizes[index] * ConstellationScale * twinkle,
                    constellation.Color,
                    0.70f + twinkle * 0.25f,
                    index % 3 == 0);
            }
        }
    }

    private void DrawConstellationLink(Vector2 from, Vector2 to, Color color, float pulse)
    {
        Color glow = color;
        glow.A = ConstellationLinkAlpha * 0.13f * pulse;
        DrawLine(from, to, glow, 7.5f, true);

        Color line = color.Lerp(Colors.White, 0.18f);
        line.A = ConstellationLinkAlpha * pulse;
        DrawLine(from, to, line, 1.75f, true);
    }

    private void DrawTravelingConstellationPulse(
        Constellation constellation,
        IReadOnlyList<Vector2> positions)
    {
        if (constellation.Links.Length == 0)
            return;

        float travel = PositiveModulo(
            (float)_elapsed * constellation.TraceSpeed + constellation.Phase,
            constellation.Links.Length);
        int linkIndex = Math.Clamp((int)travel, 0, constellation.Links.Length - 1);
        float progress = travel - linkIndex;
        Link link = constellation.Links[linkIndex];
        Vector2 position = positions[link.From].Lerp(positions[link.To], progress);

        Color halo = constellation.Color;
        halo.A = 0.10f;
        DrawCircle(position, 18f, halo);
        DrawConstellationStar(position, 5.8f, constellation.Color, 0.92f, true);
    }

    private void DrawConstellationStar(
        Vector2 position,
        float size,
        Color color,
        float alpha,
        bool diagonalRays)
    {
        Color broadHalo = color;
        broadHalo.A = alpha * 0.055f;
        DrawCircle(position, size * 4.5f, broadHalo);

        Color innerHalo = color;
        innerHalo.A = alpha * 0.14f;
        DrawCircle(position, size * 2.25f, innerHalo);

        Color ray = color.Lerp(Colors.White, 0.28f);
        ray.A = alpha * 0.82f;
        DrawLine(position - Vector2.Up * size * 2.7f,
            position + Vector2.Up * size * 2.7f, ray,
            MathF.Max(1.25f, size * 0.22f), true);
        DrawLine(position - Vector2.Right * size * 1.65f,
            position + Vector2.Right * size * 1.65f, ray,
            MathF.Max(1.1f, size * 0.18f), true);

        if (diagonalRays)
        {
            Vector2 diagonal = new Vector2(0.82f, 0.82f) * size * 1.25f;
            Color faint = ray;
            faint.A *= 0.50f;
            DrawLine(position - diagonal, position + diagonal, faint, 1.15f, true);
            diagonal.X *= -1f;
            DrawLine(position - diagonal, position + diagonal, faint, 1.15f, true);
        }

        Color core = Colors.White.Lerp(color, 0.22f);
        core.A = alpha;
        DrawCircle(position, MathF.Max(1.8f, size * 0.33f), core);
    }

    private void DrawMeteors()
    {
        foreach (Meteor meteor in _meteors)
        {
            float lifeProgress = Math.Clamp(meteor.Age / meteor.Lifetime, 0f, 1f);
            float fade = MathF.Sin(lifeProgress * MathF.PI);
            Vector2 direction = meteor.Velocity.Normalized();
            Vector2 tail = meteor.Position - direction * meteor.TailLength;

            Color outer = meteor.Color;
            outer.A = fade * 0.08f;
            DrawLine(tail, meteor.Position, outer, meteor.Size * 4.2f, true);

            Color trail = meteor.Color.Lerp(Colors.White, 0.30f);
            trail.A = fade * 0.58f;
            DrawLine(tail, meteor.Position, trail, meteor.Size * 0.85f, true);
            DrawConstellationStar(meteor.Position, meteor.Size, meteor.Color,
                fade * 0.90f, false);
        }
    }

    private void DrawAmbientStar(AmbientStar star)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(
            (float)_elapsed * TwinkleSpeed * star.TwinkleRate + star.Phase);
        float alpha = Mathf.Lerp(0.14f, 0.72f, wave) * star.Depth;
        float size = star.Size * Mathf.Lerp(0.78f, 1.18f, wave);
        Color color = star.Color;

        Color glow = color;
        glow.A = alpha * 0.08f;
        DrawCircle(star.Position, size * 3.2f, glow);

        Color ray = color;
        ray.A = alpha * 0.42f;
        DrawLine(star.Position - Vector2.Up * size * 1.8f,
            star.Position + Vector2.Up * size * 1.8f,
            ray, MathF.Max(1f, size * 0.20f), true);
        DrawLine(star.Position - Vector2.Right * size,
            star.Position + Vector2.Right * size,
            ray, MathF.Max(1f, size * 0.18f), true);

        Color core = color;
        core.A = alpha;
        DrawCircle(star.Position, MathF.Max(1.1f, size * 0.28f), core);
    }

    private void ResetStarAtEdge(AmbientStar star, Vector2 extent)
    {
        RandomizeStar(star);
        if (_random.Randf() < 0.72f)
        {
            star.Position = new Vector2(
                _random.RandfRange(-50f, extent.X * 0.86f),
                extent.Y + _random.RandfRange(10f, 52f));
        }
        else
        {
            star.Position = new Vector2(
                _random.RandfRange(-52f, -10f),
                _random.RandfRange(extent.Y * 0.16f, extent.Y));
        }
    }

    private void RandomizeStar(AmbientStar star)
    {
        star.Depth = _random.RandfRange(0.38f, 0.90f);
        star.Size = _random.RandfRange(1.5f, 4.0f) * star.Depth;
        float speed = BaseDriftSpeed * _random.RandfRange(0.55f, 1.48f) *
            Mathf.Lerp(0.55f, 1.12f, star.Depth);
        star.Velocity = new Vector2(speed * 0.42f, -speed);
        star.TwinkleRate = _random.RandfRange(0.55f, 1.75f);
        star.Phase = _random.RandfRange(0f, Mathf.Tau);
        star.Color = Palette[_random.RandiRange(0, Palette.Length - 1)];
    }

    private void ResetMeteor(Meteor meteor, Vector2 extent, bool startVisible)
    {
        meteor.Size = _random.RandfRange(3.8f, 6.4f);
        meteor.TailLength = _random.RandfRange(105f, 220f);
        meteor.Lifetime = _random.RandfRange(2.8f, 5.2f);
        meteor.Age = startVisible
            ? _random.RandfRange(0f, meteor.Lifetime)
            : 0f;
        meteor.Position = new Vector2(
            _random.RandfRange(-extent.X * 0.10f, extent.X * 0.78f),
            _random.RandfRange(extent.Y * 0.25f, extent.Y * 1.10f));
        float speed = _random.RandfRange(95f, 175f);
        meteor.Velocity = new Vector2(speed, -speed * _random.RandfRange(0.40f, 0.72f));
        meteor.Color = Palette[_random.RandiRange(0, Palette.Length - 1)];

        if (startVisible)
            meteor.Position += meteor.Velocity * meteor.Age;
    }

    private Vector2 GetUsableSize() =>
        Size.X > 1f && Size.Y > 1f ? Size : new Vector2(1920f, 1200f);

    private static float PositiveModulo(float value, float modulus)
    {
        float result = value % modulus;
        return result < 0f ? result + modulus : result;
    }

    private readonly record struct Link(int From, int To);

    private sealed class AmbientStar
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public float Depth;
        public float TwinkleRate;
        public float Phase;
        public Color Color;
    }

    private sealed class Constellation
    {
        public Vector2 NormalizedCenter;
        public Vector2[] Pattern = [];
        public Link[] Links = [];
        public float Width;
        public float Height;
        public Vector2 DriftRadius;
        public float DriftSpeed;
        public float Phase;
        public float Rotation;
        public Color Color;
        public float TraceSpeed;
        public List<float> NodeSizes { get; } = [];
        public List<float> NodePhases { get; } = [];
    }

    private sealed class Meteor
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public float TailLength;
        public float Lifetime;
        public float Age;
        public Color Color;
    }
}
