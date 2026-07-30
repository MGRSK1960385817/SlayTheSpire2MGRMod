using Godot;

namespace SlayTheSpire2MGRMod.Characters;

/// <summary>
/// Lightweight procedural star field for the character-select portrait. The
/// effect intentionally owns no textures, so it scales cleanly with the menu
/// and cannot inherit placeholder assets from another character mod.
/// </summary>
public sealed partial class MgrCharacterSelectStars : Control
{
    [Export(PropertyHint.Range, "8,96,1")]
    public int StarCount { get; set; } = 46;

    [Export(PropertyHint.Range, "0,80,0.5")]
    public float BaseDriftSpeed { get; set; } = 13f;

    [Export(PropertyHint.Range, "0.1,4,0.05")]
    public float TwinkleSpeed { get; set; } = 1.25f;

    private static readonly Color[] Palette =
    [
        new Color("fff4c7"),
        new Color("ffd4f6"),
        new Color("d8e5ff"),
        new Color("e7d5ff"),
        new Color("bff7ff")
    ];

    private readonly List<Star> _stars = [];
    private readonly RandomNumberGenerator _random = new();
    private double _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _random.Randomize();
        RebuildStars();
        Resized += RebuildStars;
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        Resized -= RebuildStars;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        Vector2 extent = GetUsableSize();
        float dt = (float)delta;

        foreach (Star star in _stars)
        {
            star.Position += star.Velocity * dt;
            if (star.Position.X > extent.X + 40f || star.Position.Y < -40f)
                ResetAtEdge(star, extent);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (Star star in _stars)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(
                (float)_elapsed * TwinkleSpeed * star.TwinkleRate + star.Phase);
            float alpha = Mathf.Lerp(0.20f, 0.92f, wave) * star.Depth;
            float size = star.Size * Mathf.Lerp(0.78f, 1.18f, wave);
            Color color = star.Color;

            Color glow = color;
            glow.A = alpha * 0.10f;
            DrawCircle(star.Position, size * 3.4f, glow);

            Color ray = color;
            ray.A = alpha * 0.48f;
            DrawLine(
                star.Position - Vector2.Up * size * 1.8f,
                star.Position + Vector2.Up * size * 1.8f,
                ray,
                Mathf.Max(1f, size * 0.20f),
                true);
            DrawLine(
                star.Position - Vector2.Right * size,
                star.Position + Vector2.Right * size,
                ray,
                Mathf.Max(1f, size * 0.18f),
                true);

            if (star.HasDiagonalRays)
            {
                Vector2 diagonal = new Vector2(0.72f, 0.72f) * size;
                Color faintRay = ray;
                faintRay.A *= 0.55f;
                DrawLine(star.Position - diagonal, star.Position + diagonal,
                    faintRay, 1f, true);
                diagonal.X *= -1f;
                DrawLine(star.Position - diagonal, star.Position + diagonal,
                    faintRay, 1f, true);
            }

            Color core = color;
            core.A = alpha;
            DrawCircle(star.Position, Mathf.Max(1.2f, size * 0.28f), core);
        }
    }

    private void RebuildStars()
    {
        Vector2 extent = GetUsableSize();
        _stars.Clear();
        for (int index = 0; index < StarCount; index++)
        {
            var star = new Star();
            RandomizeStar(star);
            star.Position = new Vector2(
                _random.RandfRange(0f, extent.X),
                _random.RandfRange(0f, extent.Y));
            _stars.Add(star);
        }
    }

    private void ResetAtEdge(Star star, Vector2 extent)
    {
        RandomizeStar(star);
        if (_random.Randf() < 0.72f)
        {
            star.Position = new Vector2(
                _random.RandfRange(-40f, extent.X * 0.82f),
                extent.Y + _random.RandfRange(8f, 42f));
        }
        else
        {
            star.Position = new Vector2(
                _random.RandfRange(-42f, -8f),
                _random.RandfRange(extent.Y * 0.18f, extent.Y));
        }
    }

    private void RandomizeStar(Star star)
    {
        star.Depth = _random.RandfRange(0.42f, 1f);
        star.Size = _random.RandfRange(1.8f, 5.2f) * star.Depth;
        float speed = BaseDriftSpeed * _random.RandfRange(0.55f, 1.55f) *
            Mathf.Lerp(0.55f, 1.15f, star.Depth);
        star.Velocity = new Vector2(speed * 0.42f, -speed);
        star.TwinkleRate = _random.RandfRange(0.55f, 1.75f);
        star.Phase = _random.RandfRange(0f, Mathf.Tau);
        star.Color = Palette[_random.RandiRange(0, Palette.Length - 1)];
        star.HasDiagonalRays = _random.Randf() < 0.28f;
    }

    private Vector2 GetUsableSize() =>
        Size.X > 1f && Size.Y > 1f ? Size : new Vector2(1920f, 1200f);

    private sealed class Star
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public float Depth;
        public float TwinkleRate;
        public float Phase;
        public Color Color;
        public bool HasDiagonalRays;
    }
}
