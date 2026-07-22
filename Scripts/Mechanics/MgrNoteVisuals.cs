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
        bool clearAfterDelay,
        int chordAnimationIndex = 0)
    {
        NoteRack? rack = GetOrCreateRack(player, notes, capacity);
        rack?.Show(
            notes,
            capacity,
            forte,
            clearAfterDelay,
            chordAnimationIndex);
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
        int notesGeneratedBefore,
        int chordsResolvedBefore,
        bool clearAfterDelay)
    {
        NoteRack? rack = GetOrCreateRack(player, notes, capacity);
        return rack?.ShowChanneledNote(
            notes,
            capacity,
            forte,
            enteringIndex,
            notesGeneratedBefore,
            chordsResolvedBefore,
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
            bool clearAfterDelay,
            int chordAnimationIndex)
        {
            CancelScheduledClear();
            UpdateSlots(notes, capacity, forte);
            if (clearAfterDelay)
                ScheduleClear(chordAnimationIndex);
        }

        public async Task ShowChanneledNote(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte,
            int enteringIndex,
            int notesGeneratedBefore,
            int chordsResolvedBefore,
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
                {
                    _slots[enteringIndex].RandomizeIdleMotion();
                    await _slots[enteringIndex].PlayEntranceAnimation(
                        GetNoteEntranceSeconds(notesGeneratedBefore));
                }

                if (clearAfterDelay && IsValid)
                    ScheduleClear(chordsResolvedBefore);
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

        private void ScheduleClear(int chordsResolvedBefore)
        {
            _clearTween = _root.CreateTween();
            _clearTween.TweenInterval(GetChordHoldSeconds(chordsResolvedBefore));
            _clearTween.TweenCallback(Callable.From(ShowEmptySlots));
        }

        private static double GetNoteEntranceSeconds(int notesGeneratedBefore) =>
            Math.Max(
                MgrVisualTuning.Notes.MinimumNoteEntranceSeconds,
                MgrVisualTuning.Notes.FirstNoteEntranceSeconds -
                Math.Max(0, notesGeneratedBefore) *
                MgrVisualTuning.Notes.NoteEntranceAccelerationPerNote);

        private static double GetChordHoldSeconds(int chordsResolvedBefore) =>
            Math.Max(
                MgrVisualTuning.Notes.MinimumChordHoldSeconds,
                MgrVisualTuning.Notes.FirstChordHoldSeconds -
                Math.Max(0, chordsResolvedBefore) *
                MgrVisualTuning.Notes.ChordHoldAccelerationPerChord);

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
        private readonly Node2D _emptySlotOutline;
        private readonly Node2D _entranceRoot;
        private readonly MgrFloatingNoteVisual _floatingRoot;

        private NoteKind? _displayedKind;
        private Label? _amountLabel;
        private Tween? _entranceTween;

        public NoteSlot(Node parent, int index)
        {
            _anchor = new Node2D { Name = $"NoteSlot{index + 1}" };
            parent.AddChild(_anchor);

            _emptySlotOutline = CreateDashedEmptySlot();
            _anchor.AddChild(_emptySlotOutline);

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
            _emptySlotOutline.Visible = note is null;

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

        public async Task PlayEntranceAnimation(double totalSeconds)
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

            double growSeconds =
                totalSeconds * MgrVisualTuning.Notes.EntranceGrowFraction;
            double settleSeconds = Math.Max(0.01, totalSeconds - growSeconds);

            Tween tween = _anchor.CreateTween();
            _entranceTween = tween;
            tween.SetParallel();
            tween.TweenProperty(
                    _entranceRoot,
                    "position",
                    Vector2.Zero,
                    growSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                    _entranceRoot,
                    "scale",
                    Vector2.One * MgrVisualTuning.Notes.EntranceOvershootScale,
                    growSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                _entranceRoot,
                "modulate",
                Colors.White,
                growSeconds);

            tween.Chain().TweenProperty(
                    _entranceRoot,
                    "scale",
                    Vector2.One,
                    settleSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);

            bool completed = await TweenHelper.AwaitFinished(tween, _anchor);
            if (completed && ReferenceEquals(_entranceTween, tween))
                _entranceTween = null;
        }

        public void RandomizeIdleMotion()
        {
            if (_displayedKind is not null)
                _floatingRoot.RandomizeMotion();
        }

        private void CreateNote(MgrNote note)
        {
            ClearNote();

            Texture2D? texture = ResourceLoader.Load<Texture2D>(note.TexturePath);
            if (texture is null)
            {
                Entry.Logger.Warn($"Missing MGR note texture: {note.TexturePath}");
                _emptySlotOutline.Visible = true;
                return;
            }

            Color noteColor = GetOutlineColor(note.Kind);
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

        private static Node2D CreateDashedEmptySlot()
        {
            var root = new Node2D
            {
                Name = "EmptySlotDashedOutline",
                ZIndex = -1
            };
            Color color = new(0.72f, 0.76f, 0.84f, 0.58f);
            int dashCount = MgrVisualTuning.Notes.EmptySlotDashCount;
            float dashAngle =
                MathF.Tau / dashCount *
                MgrVisualTuning.Notes.EmptySlotDashFill;

            for (int index = 0; index < dashCount; index++)
            {
                float start = MathF.Tau * index / dashCount;
                float end = start + dashAngle;
                float middle = (start + end) * 0.5f;
                float radius = MgrVisualTuning.Notes.SlotRadius;
                var dash = new Line2D
                {
                    Name = $"Dash{index + 1}",
                    Points =
                    [
                        new Vector2(MathF.Cos(start), MathF.Sin(start)) * radius,
                        new Vector2(MathF.Cos(middle), MathF.Sin(middle)) * radius,
                        new Vector2(MathF.Cos(end), MathF.Sin(end)) * radius
                    ],
                    Width = MgrVisualTuning.Notes.EmptySlotDashWidth,
                    DefaultColor = color,
                    Antialiased = true,
                    JointMode = Line2D.LineJointMode.Round,
                    BeginCapMode = Line2D.LineCapMode.Round,
                    EndCapMode = Line2D.LineCapMode.Round
                };
                root.AddChild(dash);
            }

            return root;
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
            NoteKind.Ghost => new Color("a875ff"),
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
