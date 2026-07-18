using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Presentation adapter for the game-independent phrase state. Each slot and
/// filled note is a persistent node: ordinary refreshes update only what
/// changed, while newly channeled notes use a serialized entrance animation.
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
        NoteRack? rack = GetOrCreateRack(player, notes, capacity);
        rack?.Show(notes, capacity, forte, clearAfterDelay);
    }

    /// <summary>
    /// Shows one newly generated note and waits for its pop animation. Calls are
    /// serialized per rack, so effects that create several notes have an
    /// observable front-to-back order instead of appearing on one frame.
    /// </summary>
    public static Task ShowChanneledNote(
        Player player,
        IReadOnlyList<MgrNote> notes,
        int capacity,
        int forte,
        int enteringIndex,
        bool clearAfterDelay)
    {
        NoteRack? rack = GetOrCreateRack(player, notes, capacity);
        return rack?.ShowChanneledNote(
            notes,
            capacity,
            forte,
            enteringIndex,
            clearAfterDelay) ?? Task.CompletedTask;
    }

    public static void ClearAll()
    {
        foreach (NoteRack rack in Racks.Values)
            rack.Dispose();

        Racks.Clear();
    }

    private static NoteRack? GetOrCreateRack(
        Player player,
        IReadOnlyList<MgrNote> notes,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(notes);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode is null)
            return null;

        if (!Racks.TryGetValue(player, out NoteRack? rack) || !rack.IsValid)
        {
            rack?.Dispose();
            rack = new NoteRack(creatureNode);
            Racks[player] = rack;
        }

        return rack;
    }

    private sealed class NoteRack : IDisposable
    {
        private readonly Node2D _root;
        private readonly List<NoteSlot> _slots = [];
        private readonly SemaphoreSlim _channelAnimationGate = new(1, 1);
        private Tween? _clearTween;

        public bool IsValid => GodotObject.IsInstanceValid(_root) && _root.IsInsideTree();

        public NoteRack(Node parent)
        {
            _root = new Node2D
            {
                Name = "MgrNoteRack",
                Position = MgrVisualTuning.Notes.RackOffset,
                ZIndex = MgrVisualTuning.Notes.RackZIndex
            };
            parent.AddChild(_root);
        }

        public void Show(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte,
            bool clearAfterDelay)
        {
            CancelScheduledClear();
            UpdateSlots(notes, capacity, forte);
            if (clearAfterDelay)
                ScheduleClear();
        }

        public async Task ShowChanneledNote(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte,
            int enteringIndex,
            bool clearAfterDelay)
        {
            await _channelAnimationGate.WaitAsync();
            try
            {
                if (!IsValid)
                    return;

                CancelScheduledClear();
                UpdateSlots(notes, capacity, forte);

                if (enteringIndex >= 0 && enteringIndex < _slots.Count)
                    await _slots[enteringIndex].PlayEntranceAnimation();

                if (clearAfterDelay && IsValid)
                    ScheduleClear();
            }
            finally
            {
                _channelAnimationGate.Release();
            }
        }

        private void UpdateSlots(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte)
        {
            EnsureCapacity(capacity);
            for (int index = 0; index < _slots.Count; index++)
                _slots[index].Show(index < notes.Count ? notes[index] : null, forte);
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
                : MathF.Min(
                    MgrVisualTuning.Notes.DesiredSlotSpacing,
                    MgrVisualTuning.Notes.MaximumRackWidth / (capacity - 1));
            float center = (capacity - 1) * 0.5f;
            for (int index = 0; index < capacity; index++)
                _slots[index].SetPosition(new Vector2((index - center) * spacing, 0f));
        }

        private void CancelScheduledClear()
        {
            _clearTween?.Kill();
            _clearTween = null;
        }

        private void ScheduleClear()
        {
            _clearTween = _root.CreateTween();
            _clearTween.TweenInterval(MgrVisualTuning.Notes.ChordHoldSeconds);
            _clearTween.TweenCallback(Callable.From(ShowEmptySlots));
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
            CancelScheduledClear();
            if (GodotObject.IsInstanceValid(_root))
                _root.QueueFree();
        }
    }

    private sealed class NoteSlot : IDisposable
    {
        private readonly Node2D _anchor;
        private readonly Polygon2D _slotFill;
        private readonly Line2D _slotOutline;
        private readonly Line2D _slotInnerOutline;
        private readonly Label _emptyGlyph;
        private readonly Node2D _entranceRoot;
        private readonly MgrFloatingNoteVisual _floatingRoot;
        private readonly Polygon2D _entranceFlash;

        private NoteKind? _displayedKind;
        private Label? _amountLabel;
        private Tween? _entranceTween;

        public NoteSlot(Node parent, int index)
        {
            _anchor = new Node2D { Name = $"NoteSlot{index + 1}" };
            parent.AddChild(_anchor);

            Vector2[] fillPoints = CreateCirclePoints(
                MgrVisualTuning.Notes.SlotRadius,
                closeLoop: false);
            Vector2[] outlinePoints = CreateCirclePoints(
                MgrVisualTuning.Notes.SlotRadius,
                closeLoop: true);
            Vector2[] innerPoints = CreateCirclePoints(
                MgrVisualTuning.Notes.SlotRadius - 8f,
                closeLoop: true);

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
            _emptyGlyph.AddThemeColorOverride(
                "font_color",
                new Color(0.72f, 0.76f, 0.84f, 0.62f));
            _emptyGlyph.AddThemeColorOverride(
                "font_outline_color",
                new Color(0f, 0f, 0f, 0.85f));
            _emptyGlyph.AddThemeConstantOverride("outline_size", 5);
            _anchor.AddChild(_emptyGlyph);

            _entranceFlash = new Polygon2D
            {
                Name = "ChannelFlash",
                Polygon = CreateCirclePoints(
                    MgrVisualTuning.Notes.SlotRadius + 4f,
                    closeLoop: false),
                Color = new Color(1f, 1f, 1f, 0f),
                ZIndex = -1
            };
            _anchor.AddChild(_entranceFlash);

            _entranceRoot = new Node2D { Name = "FilledNoteEntrance" };
            _anchor.AddChild(_entranceRoot);

            _floatingRoot = new MgrFloatingNoteVisual { Name = "FilledNoteIdle" };
            _floatingRoot.Initialize(index);
            _entranceRoot.AddChild(_floatingRoot);
        }

        public void SetPosition(Vector2 position)
        {
            _anchor.Position = position;
        }

        public void Show(MgrNote? note, int forte)
        {
            _emptyGlyph.Visible = note is null;
            _slotOutline.DefaultColor = note is null
                ? new Color(0.78f, 0.82f, 0.9f, 0.9f)
                : GetOutlineColor(note.Kind).Lightened(0.2f);

            if (note is null)
            {
                ClearNote();
                return;
            }

            if (_displayedKind != note.Kind || _amountLabel is null)
                CreateNote(note);

            if (_amountLabel is not null)
                _amountLabel.Text = note.GetEffectAmount(forte).ToString();
        }

        public async Task PlayEntranceAnimation()
        {
            if (_displayedKind is null ||
                !GodotObject.IsInstanceValid(_entranceRoot) ||
                !_entranceRoot.IsInsideTree())
            {
                return;
            }

            _entranceTween?.Kill();
            _entranceRoot.Position = new Vector2(
                0f,
                MgrVisualTuning.Notes.EntranceStartYOffset);
            _entranceRoot.Scale = Vector2.One *
                MgrVisualTuning.Notes.EntranceStartScale;
            _entranceRoot.Modulate = new Color(1f, 1f, 1f, 0f);

            Color flashColor = GetOutlineColor(_displayedKind.Value);
            flashColor.A = MgrVisualTuning.Notes.EntranceFlashAlpha;
            _entranceFlash.Color = flashColor;
            _entranceFlash.Modulate = Colors.White;
            _entranceFlash.Scale = new Vector2(0.72f, 0.72f);

            Tween tween = _anchor.CreateTween();
            _entranceTween = tween;
            tween.SetParallel();
            tween.TweenProperty(
                    _entranceRoot,
                    "position",
                    Vector2.Zero,
                    MgrVisualTuning.Notes.EntranceGrowSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                    _entranceRoot,
                    "scale",
                    Vector2.One * MgrVisualTuning.Notes.EntranceOvershootScale,
                    MgrVisualTuning.Notes.EntranceGrowSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                _entranceRoot,
                "modulate",
                Colors.White,
                MgrVisualTuning.Notes.EntranceGrowSeconds);
            tween.TweenProperty(
                    _entranceFlash,
                    "scale",
                    Vector2.One * MgrVisualTuning.Notes.EntranceFlashScale,
                    MgrVisualTuning.Notes.EntranceGrowSeconds +
                    MgrVisualTuning.Notes.EntranceSettleSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                _entranceFlash,
                "modulate:a",
                0f,
                MgrVisualTuning.Notes.EntranceGrowSeconds +
                MgrVisualTuning.Notes.EntranceSettleSeconds);

            tween.Chain().TweenProperty(
                    _entranceRoot,
                    "scale",
                    Vector2.One,
                    MgrVisualTuning.Notes.EntranceSettleSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);

            bool completed = await TweenHelper.AwaitFinished(tween, _anchor);
            if (completed && ReferenceEquals(_entranceTween, tween))
                _entranceTween = null;
        }

        private void CreateNote(MgrNote note)
        {
            ClearNote();

            Texture2D? texture = ResourceLoader.Load<Texture2D>(note.TexturePath);
            if (texture is null)
            {
                Entry.Logger.Warn($"Missing MGR note texture: {note.TexturePath}");
                _emptyGlyph.Visible = true;
                return;
            }

            Color noteColor = GetOutlineColor(note.Kind);
            var glow = new Sprite2D
            {
                Name = "NoteGlow",
                Texture = texture,
                Scale = MgrVisualTuning.Notes.ArtworkScale * 1.18f,
                Modulate = new Color(noteColor.R, noteColor.G, noteColor.B, 0.24f),
                ZIndex = -1
            };
            _floatingRoot.AddChild(glow);

            var sprite = new Sprite2D
            {
                Name = $"{note.Name}Note",
                Texture = texture,
                Scale = MgrVisualTuning.Notes.ArtworkScale
            };
            _floatingRoot.AddChild(sprite);

            _amountLabel = new Label
            {
                Name = "EffectAmount",
                Position = new Vector2(-36f, 21f),
                Size = new Vector2(72f, 36f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _amountLabel.AddThemeFontSizeOverride("font_size", 24);
            _amountLabel.AddThemeColorOverride("font_color", Colors.White);
            _amountLabel.AddThemeColorOverride("font_outline_color", noteColor);
            _amountLabel.AddThemeColorOverride(
                "font_shadow_color",
                new Color(0f, 0f, 0f, 0.9f));
            _amountLabel.AddThemeConstantOverride("outline_size", 8);
            _amountLabel.AddThemeConstantOverride("shadow_outline_size", 2);
            sprite.AddChild(_amountLabel);

            _displayedKind = note.Kind;
        }

        private static Vector2[] CreateCirclePoints(float radius, bool closeLoop)
        {
            int segments = MgrVisualTuning.Notes.CircleSegments;
            int pointCount = segments + (closeLoop ? 1 : 0);
            var points = new Vector2[pointCount];
            for (int index = 0; index < pointCount; index++)
            {
                float angle = MathF.Tau * (index % segments) / segments;
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
            _entranceTween?.Kill();
            _entranceTween = null;
            _displayedKind = null;
            _amountLabel = null;
            _entranceRoot.Position = Vector2.Zero;
            _entranceRoot.Scale = Vector2.One;
            _entranceRoot.Modulate = Colors.White;
            _entranceFlash.Color = new Color(1f, 1f, 1f, 0f);
            _entranceFlash.Modulate = Colors.White;
            _entranceFlash.Scale = Vector2.One;

            foreach (Node child in _floatingRoot.GetChildren())
            {
                _floatingRoot.RemoveChild(child);
                child.QueueFree();
            }
        }

        public void Dispose()
        {
            _entranceTween?.Kill();
            _entranceTween = null;
            if (GodotObject.IsInstanceValid(_anchor))
                _anchor.QueueFree();
        }
    }
}
