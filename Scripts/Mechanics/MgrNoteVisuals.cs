using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Presentation adapter for the game-independent phrase state. Each slot and
/// filled note is a persistent node: ordinary refreshes update only what
/// changed, while newly channeled notes use a serialized entrance animation.
/// </summary>
public static class MgrNoteVisuals
{
    private static readonly Dictionary<Player, NoteRack> Racks = [];
    private static readonly Dictionary<Player, bool> PerformingStates = [];

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
        PerformingStates.Clear();
    }

    public static void SetPerforming(Player player, bool isPerforming)
    {
        PerformingStates[player] = isPerforming;
        if (Racks.TryGetValue(player, out NoteRack? rack) && rack.IsValid)
            rack.SetPerforming(isPerforming);
    }

    /// <summary>
    /// Replays the filled-slot chord beat for each additional resolution of the
    /// same chord. The original fill animation already presents trigger one;
    /// this method makes trigger two and beyond visually explicit.
    /// </summary>
    public static Task PlayRepeatedChordTrigger(
        Player player,
        IReadOnlyList<MgrNote> notes,
        int capacity,
        int forte,
        int chordTriggersBefore)
    {
        NoteRack? rack = GetOrCreateRack(player, notes, capacity);
        return rack?.PlayRepeatedChordTrigger(
            notes,
            capacity,
            forte,
            chordTriggersBefore) ?? Task.CompletedTask;
    }

    public static void FinishRepeatedChordTrigger(
        Player player,
        int chordTriggersBefore)
    {
        if (Racks.TryGetValue(player, out NoteRack? rack) && rack.IsValid)
            rack.FinishRepeatedChordTrigger(chordTriggersBefore);
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
            rack.SetPerforming(PerformingStates.GetValueOrDefault(player));
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
        private bool _disposed;
        private bool _isPerforming;
        private NOverlayStack? _overlayStack;
        private NCapstoneContainer? _capstoneContainer;
        private NMapScreen? _mapScreen;
        private NPeekButton? _peekButton;

        public bool IsValid =>
            !_disposed &&
            GodotObject.IsInstanceValid(_root) &&
            _root.IsInsideTree();

        public NoteRack(Node parent)
        {
            _root = new Node2D
            {
                Name = "MgrNoteRack",
                Position = MgrVisualTuning.Notes.RackOffset,
                ZIndex = MgrVisualTuning.Notes.RackZIndex
            };
            parent.AddChild(_root);
            ActiveScreenContext.Instance.Updated += OnActiveScreenContextUpdated;
            EnsureScreenVisibilitySubscriptions();
        }

        public void Show(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte,
            bool clearAfterDelay,
            int chordAnimationIndex)
        {
            EnsureScreenVisibilitySubscriptions();
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
            EnsureScreenVisibilitySubscriptions();
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
                {
                    foreach (NoteSlot slot in _slots)
                        slot.PlayChordTriggerAnimation();
                    ScheduleClear(chordsResolvedBefore);
                }
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
            {
                var slot = new NoteSlot(_root, _slots.Count);
                slot.SetPerforming(_isPerforming);
                _slots.Add(slot);
            }

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

        public void SetPerforming(bool isPerforming)
        {
            _isPerforming = isPerforming;
            foreach (NoteSlot slot in _slots)
                slot.SetPerforming(isPerforming);
        }

        public async Task PlayRepeatedChordTrigger(
            IReadOnlyList<MgrNote> notes,
            int capacity,
            int forte,
            int chordTriggersBefore)
        {
            if (!IsValid)
                return;

            // The first chord scheduled its ordinary clear as soon as the slots
            // filled. Hold (or restore) that same snapshot so a slower gameplay
            // effect cannot erase the second visible beat before it starts.
            CancelScheduledClear();
            UpdateSlots(notes, capacity, forte);
            foreach (NoteSlot slot in _slots)
                slot.PlayChordTriggerAnimation(emphasized: true);

            Tween beat = _root.CreateTween();
            beat.TweenInterval(GetRepeatedChordBeatSeconds(chordTriggersBefore));
            await TweenHelper.AwaitFinished(beat, _root);
        }

        public void FinishRepeatedChordTrigger(int chordTriggersBefore)
        {
            if (!IsValid)
                return;

            CancelScheduledClear();
            ScheduleClear(chordTriggersBefore);
        }

        private void EnsureScreenVisibilitySubscriptions()
        {
            NOverlayStack? currentStack = NOverlayStack.Instance;
            if (!ReferenceEquals(_overlayStack, currentStack))
            {
                if (_overlayStack is not null &&
                    GodotObject.IsInstanceValid(_overlayStack))
                {
                    _overlayStack.Changed -= OnOverlayStackChanged;
                }

                _overlayStack = currentStack;
                if (_overlayStack is not null &&
                    GodotObject.IsInstanceValid(_overlayStack))
                {
                    _overlayStack.Changed += OnOverlayStackChanged;
                }
            }

            EnsurePeekButtonSubscription();

            NCapstoneContainer? currentCapstone = NCapstoneContainer.Instance;
            if (!ReferenceEquals(_capstoneContainer, currentCapstone))
            {
                if (_capstoneContainer is not null &&
                    GodotObject.IsInstanceValid(_capstoneContainer))
                {
                    _capstoneContainer.Changed -= OnCapstoneChanged;
                }

                _capstoneContainer = currentCapstone;
                if (_capstoneContainer is not null &&
                    GodotObject.IsInstanceValid(_capstoneContainer))
                {
                    _capstoneContainer.Changed += OnCapstoneChanged;
                }
            }

            NMapScreen? currentMap = NMapScreen.Instance;
            if (!ReferenceEquals(_mapScreen, currentMap))
            {
                if (_mapScreen is not null && GodotObject.IsInstanceValid(_mapScreen))
                {
                    _mapScreen.Opened -= OnMapVisibilityChanged;
                    _mapScreen.Closed -= OnMapVisibilityChanged;
                }

                _mapScreen = currentMap;
                if (_mapScreen is not null && GodotObject.IsInstanceValid(_mapScreen))
                {
                    _mapScreen.Opened += OnMapVisibilityChanged;
                    _mapScreen.Closed += OnMapVisibilityChanged;
                }
            }

            RefreshScreenVisibility();
        }

        private void OnOverlayStackChanged() => EnsureScreenVisibilitySubscriptions();

        private void OnCapstoneChanged() => RefreshScreenVisibility();

        private void OnMapVisibilityChanged() => RefreshScreenVisibility();

        private void OnActiveScreenContextUpdated() => RefreshScreenVisibility();

        private void OnPeekToggled(NPeekButton _) => RefreshScreenVisibility();

        private void EnsurePeekButtonSubscription()
        {
            NPeekButton? currentPeekButton = null;
            if (_overlayStack?.Peek() is Node overlayNode &&
                GodotObject.IsInstanceValid(overlayNode))
            {
                currentPeekButton = FindPeekButton(overlayNode);
            }

            if (ReferenceEquals(_peekButton, currentPeekButton))
                return;

            if (_peekButton is not null && GodotObject.IsInstanceValid(_peekButton))
                _peekButton.Toggled -= OnPeekToggled;

            _peekButton = currentPeekButton;
            if (_peekButton is not null && GodotObject.IsInstanceValid(_peekButton))
                _peekButton.Toggled += OnPeekToggled;
        }

        private static NPeekButton? FindPeekButton(Node node)
        {
            if (node is NPeekButton peekButton)
                return peekButton;

            foreach (Node child in node.GetChildren())
            {
                NPeekButton? result = FindPeekButton(child);
                if (result is not null)
                    return result;
            }

            return null;
        }

        private void RefreshScreenVisibility()
        {
            // Quick SL frees the old combat room without running the ordinary
            // combat-end hooks. The rack can therefore remain subscribed to
            // screen events after its Godot node has already been destroyed.
            // Detach that stale rack before touching the disposed node.
            if (!IsValid)
            {
                Dispose();
                return;
            }

            bool hasOverlay =
                _overlayStack is not null &&
                GodotObject.IsInstanceValid(_overlayStack) &&
                _overlayStack.ScreenCount > 0 &&
                !(_peekButton is not null &&
                  GodotObject.IsInstanceValid(_peekButton) &&
                  _peekButton.IsPeeking);
            bool hasCapstone =
                _capstoneContainer is not null &&
                GodotObject.IsInstanceValid(_capstoneContainer) &&
                _capstoneContainer.InUse;
            bool hasOpenMap =
                _mapScreen is not null &&
                GodotObject.IsInstanceValid(_mapScreen) &&
                _mapScreen.IsOpen;
            bool hasOpenRelicInspection =
                NGame.Instance?.InspectRelicScreen is { Visible: true };
            bool shouldShow =
                !hasOverlay &&
                !hasCapstone &&
                !hasOpenMap &&
                !hasOpenRelicInspection;

            _root.Visible = shouldShow;
            if (!shouldShow)
            {
                foreach (NoteSlot slot in _slots)
                    slot.HideHoverTipForScreen();
            }
        }

        private void CancelScheduledClear()
        {
            if (_clearTween is not null &&
                GodotObject.IsInstanceValid(_clearTween))
            {
                _clearTween.Kill();
            }

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

        private static double GetRepeatedChordBeatSeconds(int chordTriggersBefore) =>
            Math.Max(
                MgrVisualTuning.Notes.MinimumRepeatedChordBeatSeconds,
                MgrVisualTuning.Notes.RepeatedChordBeatSeconds -
                Math.Max(0, chordTriggersBefore) *
                MgrVisualTuning.Notes.RepeatedChordBeatAccelerationPerTrigger);

        private void ShowEmptySlots()
        {
            if (!GodotObject.IsInstanceValid(_root))
                return;

            foreach (NoteSlot slot in _slots)
                slot.Show(note: null, forte: 0);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CancelScheduledClear();
            ActiveScreenContext.Instance.Updated -= OnActiveScreenContextUpdated;
            if (_overlayStack is not null &&
                GodotObject.IsInstanceValid(_overlayStack))
            {
                _overlayStack.Changed -= OnOverlayStackChanged;
            }

            if (_capstoneContainer is not null &&
                GodotObject.IsInstanceValid(_capstoneContainer))
            {
                _capstoneContainer.Changed -= OnCapstoneChanged;
            }

            if (_mapScreen is not null && GodotObject.IsInstanceValid(_mapScreen))
            {
                _mapScreen.Opened -= OnMapVisibilityChanged;
                _mapScreen.Closed -= OnMapVisibilityChanged;
            }

            if (_peekButton is not null && GodotObject.IsInstanceValid(_peekButton))
                _peekButton.Toggled -= OnPeekToggled;

            _overlayStack = null;
            _capstoneContainer = null;
            _mapScreen = null;
            _peekButton = null;
            if (GodotObject.IsInstanceValid(_root))
                _root.QueueFree();
        }
    }

    private sealed class NoteSlot : IDisposable
    {
        private static Shader? _noteGlowShader;

        private readonly Node2D _anchor;
        private readonly Node2D _emptySlotTransitionRoot;
        private readonly MgrRotatingNoteSlotFrame _emptySlotOutline;
        private readonly Node2D _entranceRoot;
        private readonly MgrFloatingNoteVisual _floatingRoot;
        private readonly MgrNoteBurstVisual _burst;
        private readonly Control _hoverBounds;

        private NoteKind? _displayedKind;
        private Color _noteColor = Colors.White;
        private Label? _amountLabel;
        private Tween? _entranceTween;
        private Tween? _chordTween;
        private Tween? _emptySlotTransitionTween;
        private bool _emptySlotPresented;
        private bool _isHovered;

        public NoteSlot(Node parent, int index)
        {
            _anchor = new Node2D { Name = $"NoteSlot{index + 1}" };
            parent.AddChild(_anchor);

            _emptySlotTransitionRoot = new Node2D
            {
                Name = "EmptySlotTransition"
            };
            _anchor.AddChild(_emptySlotTransitionRoot);

            _emptySlotOutline = CreateDashedEmptySlot(index);
            _emptySlotTransitionRoot.AddChild(_emptySlotOutline);

            _burst = new MgrNoteBurstVisual
            {
                Name = "NoteGlowAndStars"
            };
            _anchor.AddChild(_burst);

            _entranceRoot = new Node2D { Name = "FilledNoteEntrance" };
            _anchor.AddChild(_entranceRoot);

            _floatingRoot = new MgrFloatingNoteVisual { Name = "FilledNoteIdle" };
            _floatingRoot.Initialize(index);
            _entranceRoot.AddChild(_floatingRoot);

            float hoverRadius = MgrVisualTuning.Notes.SlotRadius + 13f;
            _hoverBounds = new Control
            {
                Name = "HoverBounds",
                Position = Vector2.One * -hoverRadius,
                Size = Vector2.One * hoverRadius * 2f,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                ZIndex = 20
            };
            _hoverBounds.MouseEntered += OnMouseEntered;
            _hoverBounds.MouseExited += OnMouseExited;
            _anchor.AddChild(_hoverBounds);
        }

        public void SetPosition(Vector2 position)
        {
            _anchor.Position = position;
        }

        public void SetPerforming(bool isPerforming) =>
            _emptySlotOutline.SetPerforming(isPerforming);

        public void Show(MgrNote? note, int forte)
        {
            if (note is null)
            {
                bool shouldAnimate = _displayedKind is not null ||
                    !_emptySlotPresented;
                ClearNote();
                if (shouldAnimate)
                    PlayEmptySlotAppearAnimation();
                RefreshHoverTip();
                return;
            }

            bool replacesEmptySlot = _displayedKind is null;
            if (_displayedKind != note.Kind || _amountLabel is null)
                CreateNote(note);

            if (replacesEmptySlot && _displayedKind is not null)
                PlayEmptySlotCollapseAnimation();

            if (_amountLabel is not null)
                _amountLabel.Text = GetDisplayedAmount(note, forte).ToString();

            RefreshHoverTip();
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
            Color filledTint = MgrVisualTuning.Notes.FilledNoteTint;
            _entranceRoot.Modulate = new Color(
                filledTint.R,
                filledTint.G,
                filledTint.B,
                0f);
            _burst.Burst(_noteColor, MgrNoteBurstStyle.Entrance);

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
                filledTint,
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

        public void PlayChordTriggerAnimation(bool emphasized = false)
        {
            if (_displayedKind is null ||
                !GodotObject.IsInstanceValid(_entranceRoot) ||
                !_entranceRoot.IsInsideTree())
            {
                return;
            }

            _burst.Burst(
                _noteColor,
                emphasized
                    ? MgrNoteBurstStyle.RepeatedChord
                    : MgrNoteBurstStyle.Chord);
            _chordTween?.Kill();
            _entranceRoot.Scale = Vector2.One;
            Color filledTint = MgrVisualTuning.Notes.FilledNoteTint;
            Color peakTint = emphasized
                ? new Color(1f, 1f, 1f, 1f)
                : filledTint;
            float peakScale = emphasized
                ? MgrVisualTuning.Notes.RepeatedChordTriggerScale
                : MgrVisualTuning.Notes.ChordTriggerScale;
            Tween tween = _anchor.CreateTween().SetParallel();
            _chordTween = tween;
            tween.TweenProperty(
                    _entranceRoot,
                    "scale",
                    Vector2.One * peakScale,
                    MgrVisualTuning.Notes.ChordTriggerGrowSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                _entranceRoot,
                "modulate",
                peakTint,
                MgrVisualTuning.Notes.ChordTriggerGrowSeconds);
            tween.Chain();
            tween.TweenProperty(
                    _entranceRoot,
                    "scale",
                    Vector2.One,
                    MgrVisualTuning.Notes.ChordTriggerSettleSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                _entranceRoot,
                "modulate",
                filledTint,
                MgrVisualTuning.Notes.ChordTriggerSettleSeconds);
            tween.TweenCallback(Callable.From(() =>
            {
                if (ReferenceEquals(_chordTween, tween))
                    _chordTween = null;
            }));
        }

        private void PlayEmptySlotCollapseAnimation()
        {
            _emptySlotPresented = false;
            _emptySlotTransitionTween?.Kill();
            _emptySlotTransitionRoot.Visible = true;
            _emptySlotTransitionRoot.Scale = Vector2.One;
            _emptySlotTransitionRoot.Rotation = 0f;
            _emptySlotTransitionRoot.Modulate = Colors.White;
            _burst.Burst(
                MgrVisualTuning.Performances.PerformanceAccentColor,
                MgrNoteBurstStyle.SlotTransition);

            Tween tween = _anchor.CreateTween().SetParallel();
            _emptySlotTransitionTween = tween;
            tween.TweenProperty(
                    _emptySlotTransitionRoot,
                    "scale",
                    Vector2.One * 0.04f,
                    MgrVisualTuning.Notes.EmptySlotCollapseSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                    _emptySlotTransitionRoot,
                    "rotation",
                    MgrVisualTuning.Notes.EmptySlotTransitionRotation,
                    MgrVisualTuning.Notes.EmptySlotCollapseSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                _emptySlotTransitionRoot,
                "modulate",
                new Color(1f, 1f, 1f, 0f),
                MgrVisualTuning.Notes.EmptySlotCollapseSeconds);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (!ReferenceEquals(_emptySlotTransitionTween, tween) ||
                    !GodotObject.IsInstanceValid(_emptySlotTransitionRoot))
                {
                    return;
                }

                _emptySlotTransitionRoot.Visible = false;
                _emptySlotTransitionTween = null;
            }));
        }

        private void PlayEmptySlotAppearAnimation()
        {
            _emptySlotPresented = true;
            _emptySlotTransitionTween?.Kill();
            _emptySlotTransitionRoot.Visible = true;
            _emptySlotTransitionRoot.Scale = Vector2.One * 0.04f;
            _emptySlotTransitionRoot.Rotation =
                -MgrVisualTuning.Notes.EmptySlotTransitionRotation;
            _emptySlotTransitionRoot.Modulate = new Color(1f, 1f, 1f, 0f);
            _burst.Burst(
                MgrVisualTuning.Performances.PerformanceAccentColor,
                MgrNoteBurstStyle.SlotTransition);

            double growSeconds =
                MgrVisualTuning.Notes.EmptySlotAppearSeconds * 0.72;
            double settleSeconds = Math.Max(
                0.01,
                MgrVisualTuning.Notes.EmptySlotAppearSeconds - growSeconds);
            Tween tween = _anchor.CreateTween().SetParallel();
            _emptySlotTransitionTween = tween;
            tween.TweenProperty(
                    _emptySlotTransitionRoot,
                    "scale",
                    Vector2.One *
                        MgrVisualTuning.Notes.EmptySlotAppearOvershootScale,
                    growSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                    _emptySlotTransitionRoot,
                    "rotation",
                    0f,
                    growSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                _emptySlotTransitionRoot,
                "modulate",
                Colors.White,
                growSeconds);
            tween.Chain().TweenProperty(
                    _emptySlotTransitionRoot,
                    "scale",
                    Vector2.One,
                    settleSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenCallback(Callable.From(() =>
            {
                if (ReferenceEquals(_emptySlotTransitionTween, tween))
                    _emptySlotTransitionTween = null;
            }));
        }

        private void CreateNote(MgrNote note)
        {
            ClearNote();

            Sprite2D sprite;
            if (note.Kind == NoteKind.OmniaNote)
            {
                var omniaNoteVisual = new MgrOmniaNoteVisual
                {
                    Name = $"{note.Name}Note"
                };
                if (!omniaNoteVisual.Initialize())
                {
                    omniaNoteVisual.QueueFree();
                    _emptySlotOutline.Visible = true;
                    return;
                }

                sprite = omniaNoteVisual;
            }
            else
            {
                Texture2D? texture = ResourceLoader.Load<Texture2D>(note.TexturePath);
                if (texture is null)
                {
                    Entry.Logger.Warn($"Missing MGR note texture: {note.TexturePath}");
                    _emptySlotOutline.Visible = true;
                    return;
                }

                sprite = note.Kind == NoteKind.Ghost
                    ? new MgrGhostNoteVisual()
                    : new Sprite2D();
                sprite.Name = $"{note.Name}Note";
                sprite.Texture = texture;
                sprite.Scale = GetArtworkScale(texture);
            }

            Color noteColor = GetOutlineColor(note.Kind);
            _noteColor = noteColor;
            if (note.Kind != NoteKind.OmniaNote)
                sprite.Material = CreateNoteGlowMaterial(noteColor, sprite.Texture!);
            _floatingRoot.AddChild(sprite);

            _amountLabel = new Label
            {
                Name = "EffectAmount",
                Position = MgrVisualTuning.Notes.AmountLabelPosition,
                Size = MgrVisualTuning.Notes.AmountLabelSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _amountLabel.Visible = true;
            _amountLabel.AddThemeFontSizeOverride(
                "font_size",
                MgrVisualTuning.Notes.AmountLabelFontSize);
            _amountLabel.AddThemeColorOverride("font_color", Colors.White);
            _amountLabel.AddThemeColorOverride(
                "font_outline_color",
                noteColor);
            _amountLabel.AddThemeColorOverride(
                "font_shadow_color",
                new Color(0f, 0f, 0f, 0.9f));
            _amountLabel.AddThemeConstantOverride(
                "outline_size",
                MgrVisualTuning.Notes.AmountLabelOutlineSize);
            _amountLabel.AddThemeConstantOverride("shadow_outline_size", 2);
            // Keep the amount independent from the source artwork resolution.
            // It still follows the shared floating/entrance roots, but is no
            // longer scaled by a 64px/384px Sprite2D.
            _floatingRoot.AddChild(_amountLabel);

            _displayedKind = note.Kind;
        }

        private static int GetDisplayedAmount(MgrNote note, int forte)
        {
            // Ghost and Omnia use a fixed visual marker. The displayed "1" is
            // intentionally independent from their compound/mechanical effects.
            return note.Kind is NoteKind.Ghost or NoteKind.OmniaNote
                ? 1
                : note.GetEffectAmount(forte);
        }

        private void OnMouseEntered()
        {
            _isHovered = true;
            ShowHoverTip();
        }

        private void OnMouseExited()
        {
            _isHovered = false;
            NHoverTipSet.Remove(_hoverBounds);
        }

        public void HideHoverTipForScreen()
        {
            _isHovered = false;
            NHoverTipSet.Remove(_hoverBounds);
        }

        private void RefreshHoverTip()
        {
            if (!_isHovered)
                return;

            NHoverTipSet.Remove(_hoverBounds);
            ShowHoverTip();
        }

        private void ShowHoverTip()
        {
            string keywordId = _displayedKind switch
            {
                NoteKind.Attack => MgrKeywords.AttackNote,
                NoteKind.Skill => MgrKeywords.SkillNote,
                NoteKind.Power => MgrKeywords.PowerNote,
                NoteKind.Status => MgrKeywords.StatusNote,
                NoteKind.Curse => MgrKeywords.CurseNote,
                NoteKind.Starry => MgrKeywords.StarryNote,
                NoteKind.Ghost => MgrKeywords.GhostNote,
                NoteKind.OmniaNote => MgrKeywords.OmniaNote,
                _ => MgrKeywords.EmptyNoteSlot
            };

            IHoverTip tip = HoverTipFactory.FromKeyword(
                keywordId.GetModCardKeyword());
            NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(
                    _hoverBounds,
                    [tip],
                    HoverTip.GetHoverTipAlignment(_hoverBounds));
            if (hoverTipSet is null)
                return;

            // The Note rack deliberately has a raised combat Z index. Native
            // hover tips otherwise inherit a lower canvas order and can appear
            // behind the Note artwork. Make this popup absolute and topmost,
            // matching the visual precedence of vanilla Orb hover tips.
            hoverTipSet.ZAsRelative = false;
            hoverTipSet.ZIndex = 4096;
            hoverTipSet.SetFollowOwner();
        }

        private static MgrRotatingNoteSlotFrame CreateDashedEmptySlot(int slotIndex)
        {
            var root = new MgrRotatingNoteSlotFrame
            {
                Name = "EmptySlotDashedOutline",
                ZIndex = -1
            };
            root.Initialize(slotIndex);
            Color color = MgrVisualTuning.Notes.EmptySlotBaseColor;
            color.A = MgrVisualTuning.Notes.EmptySlotBaseAlpha;
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
            NoteKind.Curse => MgrVisualTuning.Notes.CurseAccentColor,
            NoteKind.Starry => new Color("f020c8"),
            NoteKind.Ghost => new Color("a875ff"),
            NoteKind.OmniaNote => Colors.White,
            _ => Colors.Black
        };

        private static ShaderMaterial CreateNoteGlowMaterial(
            Color glowColor,
            Texture2D noteTexture)
        {
            Shader shader = _noteGlowShader ??= new Shader
            {
                Code = """
                    shader_type canvas_item;

                    uniform sampler2D note_texture : source_color, filter_linear, repeat_disable;
                    uniform vec4 glow_color : source_color = vec4(1.0);
                    uniform float glow_radius_ratio = 0.035;
                    uniform float glow_strength = 0.38;
                    uniform float canvas_margin_ratio = 0.06;

                    float uv_mask(vec2 uv) {
                        return step(0.0, uv.x) * step(uv.x, 1.0) *
                            step(0.0, uv.y) * step(uv.y, 1.0);
                    }

                    vec4 sample_source(vec2 uv) {
                        return texture(note_texture, clamp(uv, vec2(0.0), vec2(1.0))) *
                            uv_mask(uv);
                    }

                    void vertex() {
                        // Expand the Sprite quad before sampling. Some note art
                        // touches its PNG edge; without this margin a shader
                        // glow would be cut off by the original rectangle.
                        float expansion = 1.0 + canvas_margin_ratio * 2.0;
                        VERTEX *= expansion;
                        UV = (UV - vec2(0.5)) * expansion + vec2(0.5);
                    }

                    float sampled_alpha(vec2 uv, float radius) {
                        float diagonal = radius * 0.70710678;
                        float alpha = 0.0;
                        alpha = max(alpha, sample_source(uv + vec2(radius, 0.0)).a);
                        alpha = max(alpha, sample_source(uv + vec2(-radius, 0.0)).a);
                        alpha = max(alpha, sample_source(uv + vec2(0.0, radius)).a);
                        alpha = max(alpha, sample_source(uv + vec2(0.0, -radius)).a);
                        alpha = max(alpha, sample_source(uv + vec2(diagonal, diagonal)).a);
                        alpha = max(alpha, sample_source(uv + vec2(-diagonal, diagonal)).a);
                        alpha = max(alpha, sample_source(uv + vec2(diagonal, -diagonal)).a);
                        alpha = max(alpha, sample_source(uv + vec2(-diagonal, -diagonal)).a);
                        return alpha;
                    }

                    void fragment() {
                        vec4 source = sample_source(UV);
                        vec4 modulation = COLOR;
                        float outer_alpha = max(
                            0.0,
                            sampled_alpha(UV, glow_radius_ratio) - source.a);
                        // A second, tighter sample fills the space between the
                        // silhouette and the soft outer edge.
                        outer_alpha = max(
                            outer_alpha,
                            (sampled_alpha(UV, glow_radius_ratio * 0.52) - source.a) * 1.22);
                        float glow_alpha = clamp(
                            outer_alpha * glow_strength * glow_color.a,
                            0.0,
                            1.0);
                        float final_alpha = source.a + glow_alpha * (1.0 - source.a);
                        vec3 premultiplied =
                            source.rgb * source.a +
                            glow_color.rgb * glow_alpha * (1.0 - source.a);
                        vec3 final_color = final_alpha > 0.0001
                            ? premultiplied / final_alpha
                            : vec3(0.0);
                        COLOR = vec4(
                            final_color * modulation.rgb,
                            final_alpha * modulation.a);
                    }
                    """
            };
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("note_texture", noteTexture);
            material.SetShaderParameter("glow_color", glowColor);
            material.SetShaderParameter(
                "glow_radius_ratio",
                MgrVisualTuning.Notes.ArtworkGlowRadiusRatio);
            material.SetShaderParameter(
                "glow_strength",
                MgrVisualTuning.Notes.ArtworkGlowStrength);
            material.SetShaderParameter(
                "canvas_margin_ratio",
                MgrVisualTuning.Notes.ArtworkGlowCanvasMarginRatio);
            return material;
        }

        private static Vector2 GetArtworkScale(Texture2D texture)
        {
            Vector2 sourceSize = texture.GetSize();
            float longestSide = MathF.Max(sourceSize.X, sourceSize.Y);
            return longestSide > 0f
                ? Vector2.One *
                    (MgrVisualTuning.Notes.SlotRadius * 2f *
                        MgrVisualTuning.Notes.ArtworkFillRatio / longestSide)
                : Vector2.One;
        }

        private void ClearNote()
        {
            _entranceTween?.Kill();
            _entranceTween = null;
            _chordTween?.Kill();
            _chordTween = null;
            _displayedKind = null;
            _amountLabel = null;
            _entranceRoot.Position = Vector2.Zero;
            _entranceRoot.Scale = Vector2.One;
            _entranceRoot.Modulate = MgrVisualTuning.Notes.FilledNoteTint;

            foreach (Node child in _floatingRoot.GetChildren())
            {
                _floatingRoot.RemoveChild(child);
                child.QueueFree();
            }
        }

        public void Dispose()
        {
            NHoverTipSet.Remove(_hoverBounds);
            _hoverBounds.MouseEntered -= OnMouseEntered;
            _hoverBounds.MouseExited -= OnMouseExited;
            _entranceTween?.Kill();
            _entranceTween = null;
            _chordTween?.Kill();
            _chordTween = null;
            _emptySlotTransitionTween?.Kill();
            _emptySlotTransitionTween = null;
            if (GodotObject.IsInstanceValid(_anchor))
                _anchor.QueueFree();
        }
    }
}
