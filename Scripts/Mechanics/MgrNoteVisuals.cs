using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Presentation adapter for the game-independent phrase state. Slots are drawn
/// locally instead of creating model-less NOrb nodes, which have no visible
/// empty-basket texture in STS2 v0.108.0.
/// </summary>
public static class MgrNoteVisuals
{
    private static readonly Dictionary<Player, NoteRack> Racks = [];

    public static void Show(
        Player player,
        IReadOnlyList<MgrNote> notes,
        int capacity,
        int forte,
        bool clearAfterDelay)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(notes);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode is null)
            return;

        if (!Racks.TryGetValue(player, out NoteRack? rack) || !rack.IsValid)
        {
            rack?.Dispose();
            rack = new NoteRack(creatureNode);
            Racks[player] = rack;
        }

        rack.Show(notes, capacity, forte, clearAfterDelay);
    }

    public static void ClearAll()
    {
        foreach (NoteRack rack in Racks.Values)
            rack.Dispose();

        Racks.Clear();
    }

    private sealed class NoteRack : IDisposable
    {
        private const float DesiredSlotSpacing = 96f;
        private const float MaximumRackWidth = 480f;

        private readonly Node2D _root;
        private readonly List<NoteSlot> _slots = [];
        private Tween? _clearTween;

        public bool IsValid => GodotObject.IsInstanceValid(_root) && _root.IsInsideTree();

        public NoteRack(Node parent)
        {
            _root = new Node2D
            {
                Name = "MgrNoteRack",
                Position = new Vector2(0f, -430f),
                ZIndex = 50
            };
            parent.AddChild(_root);
        }

        public void Show(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte,
            bool clearAfterDelay)
        {
            _clearTween?.Kill();
            _clearTween = null;
            EnsureCapacity(capacity);

            for (int index = 0; index < _slots.Count; index++)
                _slots[index].Show(index < notes.Count ? notes[index] : null, forte);

            if (!clearAfterDelay)
                return;

            _clearTween = _root.CreateTween();
            _clearTween.TweenInterval(0.45);
            _clearTween.TweenCallback(Callable.From(ShowEmptySlots));
        }

        private void EnsureCapacity(int capacity)
        {
            while (_slots.Count < capacity)
                _slots.Add(new NoteSlot(_root, _slots.Count));

            while (_slots.Count > capacity)
            {
                int lastIndex = _slots.Count - 1;
                _slots[lastIndex].Dispose();
                _slots.RemoveAt(lastIndex);
            }

            float spacing = capacity <= 1
                ? 0f
                : MathF.Min(DesiredSlotSpacing, MaximumRackWidth / (capacity - 1));
            float center = (capacity - 1) * 0.5f;
            for (int index = 0; index < capacity; index++)
                _slots[index].SetPosition(new Vector2((index - center) * spacing, 0f));
        }

        private void ShowEmptySlots()
        {
            if (!GodotObject.IsInstanceValid(_root))
                return;

            foreach (NoteSlot slot in _slots)
                slot.Show(note: null, forte: 0);
        }

        public void Dispose()
        {
            _clearTween?.Kill();
            _clearTween = null;
            if (GodotObject.IsInstanceValid(_root))
                _root.QueueFree();
        }
    }

    private sealed class NoteSlot : IDisposable
    {
        private const float SlotRadius = 42f;
        private const int CircleSegments = 48;
        private static readonly Vector2 NoteScale = new(0.68f, 0.68f);

        private readonly Node2D _anchor;
        private readonly Polygon2D _slotFill;
        private readonly Line2D _slotOutline;
        private readonly Line2D _slotInnerOutline;
        private readonly Label _emptyGlyph;
        private readonly Node2D _noteContainer;

        public NoteSlot(Node parent, int index)
        {
            _anchor = new Node2D { Name = $"NoteSlot{index + 1}" };
            parent.AddChild(_anchor);

            Vector2[] fillPoints = CreateCirclePoints(SlotRadius, closeLoop: false);
            Vector2[] outlinePoints = CreateCirclePoints(SlotRadius, closeLoop: true);
            Vector2[] innerPoints = CreateCirclePoints(SlotRadius - 8f, closeLoop: true);

            _slotFill = new Polygon2D
            {
                Name = "SlotFill",
                Polygon = fillPoints,
                Color = new Color(0.055f, 0.06f, 0.085f, 0.68f),
                ZIndex = -3
            };
            _anchor.AddChild(_slotFill);

            _slotOutline = new Line2D
            {
                Name = "SlotOutline",
                Points = outlinePoints,
                Width = 5f,
                DefaultColor = new Color(0.78f, 0.82f, 0.9f, 0.9f),
                Antialiased = true,
                JointMode = Line2D.LineJointMode.Round,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
                ZIndex = -1
            };
            _anchor.AddChild(_slotOutline);

            _slotInnerOutline = new Line2D
            {
                Name = "SlotInnerOutline",
                Points = innerPoints,
                Width = 2f,
                DefaultColor = new Color(0.58f, 0.62f, 0.7f, 0.48f),
                Antialiased = true,
                JointMode = Line2D.LineJointMode.Round,
                ZIndex = -2
            };
            _anchor.AddChild(_slotInnerOutline);

            _emptyGlyph = new Label
            {
                Name = "EmptyNoteGlyph",
                Text = "♪",
                Position = new Vector2(-24f, -28f),
                Size = new Vector2(48f, 56f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _emptyGlyph.AddThemeFontSizeOverride("font_size", 30);
            _emptyGlyph.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.84f, 0.62f));
            _emptyGlyph.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
            _emptyGlyph.AddThemeConstantOverride("outline_size", 5);
            _anchor.AddChild(_emptyGlyph);

            _noteContainer = new Node2D { Name = "FilledNote" };
            _anchor.AddChild(_noteContainer);
        }

        public void SetPosition(Vector2 position)
        {
            _anchor.Position = position;
        }

        public void Show(MgrNote? note, int forte)
        {
            ClearNote();
            _emptyGlyph.Visible = note is null;
            _slotOutline.DefaultColor = note is null
                ? new Color(0.78f, 0.82f, 0.9f, 0.9f)
                : GetOutlineColor(note.Kind).Lightened(0.2f);

            if (note is null)
                return;

            Texture2D? texture = ResourceLoader.Load<Texture2D>(note.TexturePath);
            if (texture is null)
            {
                Entry.Logger.Warn($"Missing MGR note texture: {note.TexturePath}");
                _emptyGlyph.Visible = true;
                return;
            }

            var sprite = new Sprite2D
            {
                Name = $"{note.Name}Note",
                Texture = texture,
                Scale = NoteScale
            };
            _noteContainer.AddChild(sprite);

            var amountLabel = new Label
            {
                Name = "EffectAmount",
                Text = note.GetEffectAmount(forte).ToString(),
                Position = new Vector2(-36f, 21f),
                Size = new Vector2(72f, 36f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            amountLabel.AddThemeFontSizeOverride("font_size", 24);
            amountLabel.AddThemeColorOverride("font_color", Colors.White);
            amountLabel.AddThemeColorOverride("font_outline_color", GetOutlineColor(note.Kind));
            amountLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
            amountLabel.AddThemeConstantOverride("outline_size", 8);
            amountLabel.AddThemeConstantOverride("shadow_outline_size", 2);
            sprite.AddChild(amountLabel);
        }

        private static Vector2[] CreateCirclePoints(float radius, bool closeLoop)
        {
            int pointCount = CircleSegments + (closeLoop ? 1 : 0);
            var points = new Vector2[pointCount];
            for (int index = 0; index < pointCount; index++)
            {
                float angle = MathF.Tau * (index % CircleSegments) / CircleSegments;
                points[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }

            return points;
        }

        private static Color GetOutlineColor(NoteKind kind) => kind switch
        {
            NoteKind.Attack => new Color("ff3b30"),
            NoteKind.Skill => new Color("22d967"),
            NoteKind.Power => new Color("1f9eff"),
            NoteKind.Status => new Color("60666d"),
            NoteKind.Curse => new Color("e8bd00"),
            NoteKind.Quest => new Color("4fc9d1"),
            NoteKind.Starry => new Color("f020c8"),
            _ => Colors.Black
        };

        private void ClearNote()
        {
            foreach (Node child in _noteContainer.GetChildren())
            {
                _noteContainer.RemoveChild(child);
                child.QueueFree();
            }
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_anchor))
                _anchor.QueueFree();
        }
    }
}
