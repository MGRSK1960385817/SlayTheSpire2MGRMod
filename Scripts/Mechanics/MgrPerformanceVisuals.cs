using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Horizontal card row above the note rack. Queue index zero (the earliest
/// entrant) is deliberately placed on the right, as specified by the mechanic.
/// </summary>
public static class MgrPerformanceVisuals
{
    private static readonly Dictionary<Player, PerformanceRack> Racks = [];
    private static readonly Dictionary<CardModel, Action> PendingPlayedCallbacks = [];

    public static void Show(Player player, IReadOnlyList<MgrPerformanceEntry> entries)
    {
        PerformanceRack? rack = GetOrCreateRack(player);
        rack?.Show(entries);
    }

    /// <summary>
    /// Waits until CardModel.OnPlayWrapper has completed its result-pile routing
    /// before taking control of the real card node. AfterCardPlayed is too early:
    /// Tower 2 creates its final PlayPileTween only after that hook returns.
    /// </summary>
    public static void QueueEntryAnimationAfterPlay(
        Player player,
        IReadOnlyList<MgrPerformanceEntry> entries,
        MgrPerformanceEntry entry,
        int queuedBeforeThisTurn)
    {
        CardModel card = entry.Card;
        if (PendingPlayedCallbacks.Remove(card, out Action? oldCallback))
            card.Played -= oldCallback;

        Action? callback = null;
        callback = () =>
        {
            card.Played -= callback;
            PendingPlayedCallbacks.Remove(card);
            // Do not create an NCard replica before Tower 2 finishes routing
            // the real played card. Native NCard.FindOnTable(model) must see a
            // single candidate throughout the original play pipeline.
            try
            {
                Show(player, entries);
                QueueEntryAnimation(
                    player,
                    entry,
                    GetEntryAnimationDurationScale(queuedBeforeThisTurn));
            }
            catch (Exception exception)
            {
                // Presentation must never invalidate an otherwise resolved card
                // play. Log the UI failure while leaving the combat model valid.
                GD.PushError(
                    $"MGR Performance entry presentation failed for " +
                    $"{card.GetType().Name}: {exception}");
            }
        };

        PendingPlayedCallbacks[card] = callback;
        card.Played += callback;
    }

    /// <summary>
    /// Immediately animates a card whose pile move has already completed. This
    /// remains available for effects that enqueue a hand/generated card without
    /// resolving a normal card play.
    /// </summary>
    public static void QueueEntryAnimation(
        Player player,
        MgrPerformanceEntry entry,
        float durationScale = 1f)
    {
        if (Racks.TryGetValue(player, out PerformanceRack? rack) && rack.IsValid)
            rack.QueuePlayedCardAnimation(entry, durationScale);
    }

    private static float GetEntryAnimationDurationScale(int queuedBeforeThisTurn) =>
        MathF.Max(
            MgrVisualTuning.Performances.MinimumEntryAnimationDurationScale,
            1f - Math.Max(0, queuedBeforeThisTurn) *
                MgrVisualTuning.Performances.EntryAnimationAccelerationPerCard);

    public static Task PlayTriggerAnimation(
        Player player,
        MgrPerformanceEntry entry,
        bool consumesRemaining,
        float durationScale)
    {
        if (!Racks.TryGetValue(player, out PerformanceRack? rack) || !rack.IsValid)
            return Task.CompletedTask;

        return rack.PlayTriggerAnimation(entry, consumesRemaining, durationScale);
    }

    public static Task PlayTriggerCompletionAnimation(
        Player player,
        MgrPerformanceEntry entry,
        float durationScale)
    {
        if (!Racks.TryGetValue(player, out PerformanceRack? rack) || !rack.IsValid)
            return Task.CompletedTask;

        return rack.PlayTriggerCompletionAnimation(entry, durationScale);
    }

    public static void SetPerforming(Player player, bool isPerforming)
    {
        MgrNoteVisuals.SetPerforming(player, isPerforming);
        if (Racks.TryGetValue(player, out PerformanceRack? rack) && rack.IsValid)
            rack.SetStaffPerforming(isPerforming);
    }

    public static Task PlayExitAnimation(
        Player player,
        MgrPerformanceEntry entry,
        PileType? destinationPile,
        float durationScale = 1f)
    {
        if (!Racks.TryGetValue(player, out PerformanceRack? rack) || !rack.IsValid)
            return Task.CompletedTask;

        return rack.PlayExitAnimation(entry, destinationPile, durationScale);
    }

    public static void ClearAll()
    {
        foreach ((CardModel card, Action callback) in PendingPlayedCallbacks)
            card.Played -= callback;

        PendingPlayedCallbacks.Clear();

        foreach (PerformanceRack rack in Racks.Values)
            rack.Dispose();

        Racks.Clear();
    }

    private static PerformanceRack? GetOrCreateRack(Player player)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode is null)
            return null;

        if (!Racks.TryGetValue(player, out PerformanceRack? rack) || !rack.IsValid)
        {
            rack?.Dispose();
            rack = new PerformanceRack(creatureNode);
            Racks[player] = rack;
        }

        return rack;
    }

    private sealed class PerformanceRack : IDisposable
    {
        // Cards are intentionally larger than the old rack and overlap heavily,
        // like a row of playing cards. The exposed strip stays wide enough to
        // hover each entry even when the queue becomes long.
        private readonly Node2D _root;
        private readonly MgrPerformanceStaffVisual _staff;
        private readonly CanvasLayer _previewLayer;
        private readonly List<PerformanceCardView> _views = [];
        private NOverlayStack? _overlayStack;
        private NCapstoneContainer? _capstoneContainer;
        private NMapScreen? _mapScreen;

        public bool IsValid => GodotObject.IsInstanceValid(_root) && _root.IsInsideTree();

        public PerformanceRack(Node parent)
        {
            _root = new Node2D
            {
                Name = "MgrPerformanceRack",
                Position = MgrVisualTuning.Performances.RackOffset,
                ZIndex = MgrVisualTuning.Performances.RackZIndex
            };
            parent.AddChild(_root);

            _staff = new MgrPerformanceStaffVisual
            {
                Name = "MgrPerformanceStaff"
            };
            _root.AddChild(_staff);
            _staff.SetActive(false);

            // CardPreviewContainer owns a layout script that moves every child
            // back to the screen centre. A private canvas layer lets the rack
            // keep hover previews beside the mouse and above combat UI.
            _previewLayer = new CanvasLayer
            {
                Name = "MgrPerformancePreviewLayer",
                Layer = 90
            };
            parent.AddChild(_previewLayer);

            EnsureScreenVisibilitySubscriptions();
        }

        public void Show(IReadOnlyList<MgrPerformanceEntry> entries)
        {
            EnsureScreenVisibilitySubscriptions();
            // The staff is the permanent visual home of the queue and remains
            // visible even while no Performance cards are currently queued.
            _staff.SetActive(true);

            foreach (PerformanceCardView stale in _views
                         .Where(view => !entries.Any(
                             entry => ReferenceEquals(entry, view.Entry)))
                         .ToArray())
            {
                stale.Dispose();
                _views.Remove(stale);
            }

            bool isFilled =
                entries.Count >= MgrVisualTuning.Performances.FilledRackCardThreshold;
            float spacing = CalculateCardSpacing(entries.Count, isFilled);
            float rightEdge = isFilled
                ? (entries.Count - 1) * spacing * 0.5f
                : CalculateUnfilledRightEdge();
            var orderedViews = new List<PerformanceCardView>(entries.Count);

            for (int index = 0; index < entries.Count; index++)
            {
                // The first entry is the rightmost; newer entries extend left.
                float x = rightEdge - index * spacing;
                PerformanceCardView? view = FindView(entries[index]);
                view ??= new PerformanceCardView(
                    _root,
                    _previewLayer,
                    entries[index]);
                view.Refresh();
                view.SetPosition(new Vector2(x, 0f));
                // The earliest card is the rightmost and visually sits above
                // later cards where their bodies overlap.
                view.SetLayer(entries.Count - index);
                orderedViews.Add(view);
            }

            _views.Clear();
            _views.AddRange(orderedViews);
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

        private void OnOverlayStackChanged() => RefreshScreenVisibility();

        private void OnCapstoneChanged() => RefreshScreenVisibility();

        private void OnMapVisibilityChanged() => RefreshScreenVisibility();

        private void RefreshScreenVisibility()
        {
            bool hasOverlay =
                _overlayStack is not null &&
                GodotObject.IsInstanceValid(_overlayStack) &&
                _overlayStack.ScreenCount > 0;
            bool hasCapstone =
                _capstoneContainer is not null &&
                GodotObject.IsInstanceValid(_capstoneContainer) &&
                _capstoneContainer.InUse;
            bool hasOpenMap =
                _mapScreen is not null &&
                GodotObject.IsInstanceValid(_mapScreen) &&
                _mapScreen.IsOpen;
            bool shouldShow = !hasOverlay && !hasCapstone && !hasOpenMap;

            // The rack belongs to the combat field, not to any full-screen or
            // capstone UI. Hide both its world-space presentation and its private
            // hover-preview canvas together so neither can leak above those screens.
            _root.Visible = shouldShow;
            _previewLayer.Visible = shouldShow;
            if (!shouldShow)
            {
                foreach (PerformanceCardView view in _views)
                    view.HidePreviewForOverlay();
            }
        }

        private static float CalculateCardSpacing(int cardCount, bool isFilled)
        {
            if (cardCount <= 1)
                return 0f;

            if (!isFilled)
                return MgrVisualTuning.Performances.UnfilledCardSpacing;

            int extraCards = Math.Max(
                0,
                cardCount - MgrVisualTuning.Performances.FilledRackCardThreshold);
            float occupiedWidth = MathF.Min(
                MgrVisualTuning.Performances.FilledRackMaximumWidth,
                MgrVisualTuning.Performances.FilledRackBaseWidth +
                extraCards * MgrVisualTuning.Performances.FilledRackWidthPerExtraCard);
            return occupiedWidth / (cardCount - 1);
        }

        private static float CalculateUnfilledRightEdge()
        {
            int maximumUnfilledCount = Math.Max(
                1,
                MgrVisualTuning.Performances.FilledRackCardThreshold - 1);
            return (maximumUnfilledCount - 1) *
                MgrVisualTuning.Performances.UnfilledCardSpacing * 0.5f;
        }

        public async Task PlayTriggerAnimation(
            MgrPerformanceEntry entry,
            bool consumesRemaining,
            float durationScale)
        {
            PerformanceCardView? view = FindView(entry);
            if (view is null)
                return;

            view.SetTriggering(true);
            await _staff.PrepareTrigger(
                view.LocalCenterX - MgrVisualTuning.Performances.StaffOffset.X,
                durationScale);
            _staff.Pulse();
            await view.PlayTriggerAnimation(consumesRemaining, durationScale);
        }

        public async Task PlayTriggerCompletionAnimation(
            MgrPerformanceEntry entry,
            float durationScale)
        {
            await _staff.CompleteTrigger(durationScale);
            FindView(entry)?.SetTriggering(false);
        }

        public void SetStaffPerforming(bool isPerforming) =>
            _staff.SetPerforming(isPerforming);

        public void PulseStaff() => _staff.Pulse();

        public Task PlayExitAnimation(
            MgrPerformanceEntry entry,
            PileType? destinationPile,
            float durationScale)
        {
            PerformanceCardView? view = FindView(entry);
            if (view is null)
                return Task.CompletedTask;

            Vector2 destination = view.ViewportCenter + new Vector2(0f, -100f);
            bool hasPileDestination = false;
            if (NCombatRoom.Instance?.Ui is { } ui)
            {
                Control? pile = destinationPile switch
                {
                    PileType.Discard => ui.DiscardPile,
                    PileType.Exhaust => ui.ExhaustPile,
                    PileType.Draw => ui.DrawPile,
                    _ => null
                };

                if (pile is not null && GodotObject.IsInstanceValid(pile))
                {
                    destination = pile.GetGlobalTransformWithCanvas() *
                        (pile.Size * 0.5f);
                    hasPileDestination = true;
                }
            }

            return view.PlayExitAnimation(
                destination,
                hasPileDestination,
                durationScale);
        }

        public void QueuePlayedCardAnimation(
            MgrPerformanceEntry entry,
            float durationScale)
        {
            PerformanceCardView? destination = FindView(entry);
            if (destination is null)
                return;

            // Capture the slot now: the entry may complete and disappear while
            // the final autoplay view is still finishing its pile transition.
            TaskHelper.RunSafely(
                AwaitPlayedCardAndAnimate(
                    entry.Card,
                    destination.ViewportCenter,
                    durationScale));
        }

        private async Task AwaitPlayedCardAndAnimate(
            CardModel card,
            Vector2 destination,
            float durationScale)
        {
            // Start with an immediate lookup while AfterCardPlayed still owns the
            // play view, then allow routing a short window to reparent it.
            for (int frame = 0; frame < 30; frame++)
            {
                if (!IsValid)
                    return;

                NCard? playedCard = FindPlayedCardNode(card);
                if (playedCard is not null)
                {
                    AnimatePlayedCardTo(destination, playedCard, durationScale);
                    return;
                }

                await _root.ToSignal(_root.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            GD.PushWarning($"MGR Performance could not find the played view for {card.GetType().Name}.");
        }

        private static void AnimatePlayedCardTo(
            Vector2 destinationInViewport,
            NCard playedCard,
            float durationScale)
        {
            if (!GodotObject.IsInstanceValid(playedCard))
                return;

            playedCard.PlayPileTween?.Kill();
            playedCard.MouseFilter = Control.MouseFilterEnum.Ignore;
            playedCard.ZIndex = 250;

            Vector2 finalScale = PerformanceCardView.MiniatureScale;
            Vector2 destinationInCardCanvas =
                playedCard.GetCanvasTransform().AffineInverse() * destinationInViewport;
            Vector2 visibleCenterInCard = Vector2.Zero;
            if (playedCard.Body is { } body &&
                GodotObject.IsInstanceValid(body))
            {
                Transform2D bodyToCard =
                    playedCard.GetGlobalTransform().AffineInverse() *
                    body.GetGlobalTransform();
                visibleCenterInCard = bodyToCard * Vector2.Zero;
            }

            // Control.GlobalPosition is the root rect's position, while scaling
            // happens around PivotOffset. Account for that pivot so the visible
            // Body centre—not an assumed top-left rect—lands on the rack slot.
            Vector2 visibleCenterOffsetAtFinalScale =
                playedCard.PivotOffset +
                (visibleCenterInCard - playedCard.PivotOffset) * finalScale;
            Vector2 targetPosition =
                destinationInCardCanvas - visibleCenterOffsetAtFinalScale;
            double enterSeconds =
                MgrVisualTuning.Performances.EnterQueueSeconds *
                Math.Clamp(durationScale, 0.1f, 1f);
            var tween = playedCard.CreateTween().SetParallel();
            tween.TweenProperty(
                    playedCard,
                    "global_position",
                    targetPosition,
                    enterSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                    playedCard,
                    "scale",
                    finalScale,
                    enterSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                playedCard,
                "modulate",
                new Color(1f, 1f, 1f, 0.12f),
                enterSeconds);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(playedCard))
                    playedCard.QueueFreeSafely();
            }));
        }

        private PerformanceCardView? FindView(MgrPerformanceEntry entry) =>
            _views.FirstOrDefault(view => ReferenceEquals(view.Entry, entry));

        private NCard? FindPlayedCardNode(CardModel card)
        {
            NCard? tableCard = NCard.FindOnTable(card);
            if (IsExternalPlayedCard(tableCard, card))
                return tableCard;

            if (!_root.IsInsideTree())
                return null;

            // Result-pile VFX can temporarily reparent the real card outside
            // NCombatRoom. Search the complete scene tree as a final fallback.
            return FindPlayedCardNodeRecursive(_root.GetTree().Root, card);
        }

        private NCard? FindPlayedCardNodeRecursive(Node node, CardModel card)
        {
            if (node is NCard candidate && IsExternalPlayedCard(candidate, card))
            {
                return candidate;
            }

            foreach (Node child in node.GetChildren())
            {
                NCard? result = FindPlayedCardNodeRecursive(child, card);
                if (result is not null)
                    return result;
            }

            return null;
        }

        private bool IsExternalPlayedCard(NCard? candidate, CardModel card) =>
            candidate is not null &&
            GodotObject.IsInstanceValid(candidate) &&
            ReferenceEquals(candidate.Model, card) &&
            !_root.IsAncestorOf(candidate) &&
            !_previewLayer.IsAncestorOf(candidate);

        private void ClearViews()
        {
            foreach (PerformanceCardView view in _views)
                view.Dispose();

            _views.Clear();
        }

        public void Dispose()
        {
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

            _overlayStack = null;
            _capstoneContainer = null;
            _mapScreen = null;
            ClearViews();
            if (GodotObject.IsInstanceValid(_root))
                _root.QueueFree();
            if (GodotObject.IsInstanceValid(_previewLayer))
                _previewLayer.QueueFree();
        }
    }

    private sealed class PerformanceCardView : IDisposable
    {
        public static Vector2 MiniatureScale =>
            MgrVisualTuning.Performances.MiniatureScale;

        // STABLE COMBAT-UI INVARIANT: Tower 2's card.tscn uses zero-sized
        // NCard/CardContainer controls. Every visible card child is laid out
        // around their shared (0,0) centre. Do not replace this with Body.Size
        // or a top-left-origin rect without re-inspecting the vanilla scene.
        private static Rect2 VisibleCardRect =>
            new(-NCard.defaultSize * 0.5f, NCard.defaultSize);

        private readonly Node2D _anchor;
        private readonly Node _previewHost;
        private readonly NCard _cardNode;
        private readonly Control _cardBody;
        private readonly MgrPerformanceHoverProxy _hoverHitbox;
        private readonly ColorRect _triggerGlow;
        private readonly MgrPerformanceCardBurstVisual _triggerBurst;
        private readonly MgrPerformanceIdleEdgeVisual _idleEdge;
        private readonly MgrPerformanceCounterVisual _remainingCounter;
        private Tween? _pulseTween;
        private NCard? _hoverPreview;
        private int _baseLayer;
        private bool _isTriggering;

        public MgrPerformanceEntry Entry { get; }
        public float LocalCenterX => _anchor.Position.X;
        public Vector2 ViewportCenter =>
            _cardBody.GetGlobalTransformWithCanvas() *
            VisibleCardRect.GetCenter();

        public PerformanceCardView(
            Node parent,
            Node previewHost,
            MgrPerformanceEntry entry)
        {
            Entry = entry;
            _previewHost = previewHost;
            _anchor = new Node2D { Name = $"Performance_{entry.Card.GetType().Name}" };
            parent.AddChild(_anchor);

            _cardNode = NCard.Create(entry.Card, ModelVisibility.Visible)
                ?? throw new InvalidOperationException("Unable to create an MGR Performance card node.");
            _cardNode.Name = "Card";

            // NCard assigns Body (%CardContainer) in _Ready. NCard.Create can
            // return a pooled node that is currently outside the tree, so Body
            // is still null until it is attached to our live rack anchor.
            _anchor.AddChild(_cardNode);
            _cardBody = _cardNode.Body ??
                _cardNode.GetNodeOrNull<Control>("%CardContainer") ??
                _cardNode;

            // The visible card is already centred on the NCard origin. Keeping
            // that origin at the rack anchor gives the card, pulse, label and
            // hitbox one shared centre with no half-card correction.
            _cardNode.PivotOffset = Vector2.Zero;
            _cardNode.Position = Vector2.Zero;
            _cardNode.Scale = MiniatureScale;
            // NCard is pooled. Explicitly clear any Z state left by a previous
            // pile animation so a newly appended card cannot jump above the
            // older card that is supposed to cover it.
            _cardNode.ZIndex = 0;
            _cardNode.ZAsRelative = true;
            _cardNode.Modulate = new Color(
                1f,
                1f,
                1f,
                MgrVisualTuning.Performances.RackCardOpacity);
            _cardNode.MouseFilter = Control.MouseFilterEnum.Ignore;

            _triggerBurst = new MgrPerformanceCardBurstVisual
            {
                Name = "PerformanceStarBurst"
            };
            _anchor.AddChild(_triggerBurst);

            Vector2 unscaledGlowMargin = new(
                11f / MiniatureScale.X,
                11f / MiniatureScale.Y);
            _triggerGlow = new ColorRect
            {
                Name = "TriggerGlow",
                Position = VisibleCardRect.Position - unscaledGlowMargin,
                Size = VisibleCardRect.Size + unscaledGlowMargin * 2f,
                Color = new Color("fff0b8"),
                Modulate = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = -1
            };
            // Body is the actual visible CardContainer. Making every overlay a
            // child of it means internal NCard offsets/animation can no longer
            // separate the card face from its glow or counter.
            _cardBody.AddChild(_triggerGlow);
            _cardBody.MoveChild(_triggerGlow, 0);

            _idleEdge = new MgrPerformanceIdleEdgeVisual
            {
                Name = "PerformanceIdleEdge"
            };
            _cardBody.AddChild(_idleEdge);
            _idleEdge.Initialize(VisibleCardRect, MiniatureScale.X);

            _hoverHitbox = new MgrPerformanceHoverProxy
            {
                Name = "HoverHitbox",
                Target = _cardBody,
                TargetRect = VisibleCardRect,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                ZIndex = 20
            };
            _hoverHitbox.MouseEntered += OnMouseEntered;
            _hoverHitbox.MouseExited += OnMouseExited;
            _hoverHitbox.GuiInput += OnHoverInput;
            // Keep input in the high canvas layer as well. Creature controls and
            // combat overlays can otherwise intercept GUI hover before a child
            // Control under the creature node ever sees it.
            _previewHost.AddChild(_hoverHitbox);

            _remainingCounter = new MgrPerformanceCounterVisual
            {
                Name = "RemainingPerformanceBeat"
            };
            _anchor.AddChild(_remainingCounter);
            _remainingCounter.Initialize(
                entry.RemainingPerformanceTurns,
                NCard.defaultSize.Y * MiniatureScale.Y);

            Refresh();
        }

        public void Refresh()
        {
            _remainingCounter.Refresh(Entry.RemainingPerformanceTurns);
            if (GodotObject.IsInstanceValid(_cardNode) && _cardNode.IsNodeReady())
            {
                _cardNode.UpdateVisuals(PileType.Play, CardPreviewMode.Normal);
            }

            if (_hoverPreview is not null &&
                GodotObject.IsInstanceValid(_hoverPreview) &&
                _hoverPreview.IsNodeReady())
            {
                _hoverPreview.UpdateVisuals(
                    PileType.Play,
                    CardPreviewMode.Normal);
            }
        }

        public void SetPosition(Vector2 position)
        {
            _anchor.Position = position;
            PositionHoverHitbox();
            ReconcileHoverPresentation();
        }

        public void SetLayer(int layer)
        {
            _baseLayer = layer;
            _anchor.ZIndex = layer;
            _hoverHitbox.ZIndex = layer;
        }

        public void SetTriggering(bool isTriggering)
        {
            _isTriggering = isTriggering;
            _idleEdge.SetTriggering(isTriggering);
            if (isTriggering)
                SetHoveredPresentation(false);
            else
                ReconcileHoverPresentation();
        }

        public void HidePreviewForOverlay() => HideHoverPreview();

        public async Task PlayTriggerAnimation(
            bool consumesRemaining,
            float durationScale)
        {
            if (!GodotObject.IsInstanceValid(_anchor) || !_anchor.IsInsideTree())
                return;

            HideHoverPreview();
            _pulseTween?.Kill();
            _anchor.Scale = Vector2.One;
            SetHoveredPresentation(false);
            _triggerGlow.Modulate = new Color(1f, 1f, 1f, 0f);
            _triggerBurst.Burst();
            float clampedDurationScale = Math.Clamp(durationScale, 0.1f, 1f);
            _remainingCounter.PlayTrigger(consumesRemaining, clampedDurationScale);
            double growSeconds =
                MgrVisualTuning.Performances.TriggerGrowSeconds *
                clampedDurationScale;
            double settleSeconds =
                MgrVisualTuning.Performances.TriggerSettleSeconds *
                clampedDurationScale;

            Tween tween = _anchor.CreateTween();
            _pulseTween = tween;
            tween.TweenProperty(
                    _anchor,
                    "scale",
                    Vector2.One * MgrVisualTuning.Performances.TriggerScale,
                    growSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.Parallel().TweenProperty(
                _triggerGlow,
                "modulate",
                new Color(1f, 1f, 1f, 0.78f),
                growSeconds);
            tween.TweenProperty(
                    _anchor,
                    "scale",
                    Vector2.One,
                    settleSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.Parallel().TweenProperty(
                _triggerGlow,
                "modulate",
                new Color(1f, 1f, 1f, 0f),
                settleSeconds);

            bool completed = await TweenHelper.AwaitFinished(tween, _anchor);
            if (completed && ReferenceEquals(_pulseTween, tween))
                _pulseTween = null;
        }

        public async Task PlayExitAnimation(
            Vector2 destinationInViewport,
            bool hasPileDestination,
            float durationScale)
        {
            if (!GodotObject.IsInstanceValid(_anchor) || !_anchor.IsInsideTree())
                return;

            HideHoverPreview();
            _pulseTween?.Kill();
            _pulseTween = null;
            _hoverHitbox.MouseFilter = Control.MouseFilterEnum.Ignore;
            _anchor.ZIndex = 450;

            Vector2 destinationInRackCanvas =
                _anchor.GetCanvasTransform().AffineInverse() *
                destinationInViewport;
            float clampedDurationScale = Math.Clamp(durationScale, 0.1f, 1f);
            double exitSeconds =
                MgrVisualTuning.Performances.ExitSeconds *
                clampedDurationScale;

            Tween tween = _anchor.CreateTween().SetParallel();
            tween.TweenProperty(
                    _anchor,
                    "global_position",
                    destinationInRackCanvas,
                    exitSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                    _anchor,
                    "scale",
                    hasPileDestination ? new Vector2(0.34f, 0.34f) : new Vector2(0.82f, 0.82f),
                    exitSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                    _anchor,
                    "modulate",
                    new Color(1f, 1f, 1f, 0f),
                    0.26 * clampedDurationScale)
                .SetDelay(0.12 * clampedDurationScale);

            await TweenHelper.AwaitFinished(tween, _anchor);
        }

        private void OnMouseEntered()
        {
            SetHoveredPresentation(true);
        }

        private void OnMouseExited()
        {
            SetHoveredPresentation(false);
        }

        private void OnHoverInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseMotion)
            {
                ReconcileHoverPresentation();
                PositionHoverPreview();
            }
        }

        private void ReconcileHoverPresentation()
        {
            if (!GodotObject.IsInstanceValid(_hoverHitbox) ||
                !_hoverHitbox.Visible)
            {
                SetHoveredPresentation(false);
                return;
            }

            Vector2 localMouse = _hoverHitbox.GetLocalMousePosition();
            bool isActuallyHovered = new Rect2(
                Vector2.Zero,
                _hoverHitbox.Size).HasPoint(localMouse);
            SetHoveredPresentation(isActuallyHovered);
        }

        private void SetHoveredPresentation(bool isHovered)
        {
            isHovered &= !_isTriggering;
            _anchor.ZIndex = isHovered ? 300 : _baseLayer;
            _hoverHitbox.ZIndex = isHovered ? 300 : _baseLayer;
            _cardNode.Scale = isHovered
                ? MgrVisualTuning.Performances.HoveredMiniatureScale
                : MiniatureScale;

            if (isHovered)
                ShowHoverPreview();
            else
                HideHoverPreview();
        }

        private void ShowHoverPreview()
        {
            if (_hoverPreview is not null && GodotObject.IsInstanceValid(_hoverPreview))
                return;

            _hoverPreview = NCard.Create(Entry.Card, ModelVisibility.Visible);
            if (_hoverPreview is null)
                return;

            _hoverPreview.Name = "HoverPreview";
            _hoverPreview.MouseFilter = Control.MouseFilterEnum.Ignore;
            _hoverPreview.ZIndex = 300;
            _previewHost.AddChild(_hoverPreview);
            _hoverPreview.PivotOffset = Vector2.Zero;
            _hoverPreview.Scale = new Vector2(0.5f, 0.5f);
            _hoverPreview.UpdateVisuals(
                PileType.Play,
                CardPreviewMode.Normal);
            PositionHoverPreview();

            var tween = _hoverPreview.CreateTween();
            tween.TweenProperty(
                    _hoverPreview,
                    "scale",
                    MgrVisualTuning.Performances.PreviewScale,
                    MgrVisualTuning.Performances.PreviewGrowSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }

        private void PositionHoverPreview()
        {
            if (_hoverPreview is null || !GodotObject.IsInstanceValid(_hoverPreview))
                return;

            Vector2 mouse = _anchor.GetViewport().GetMousePosition();
            Vector2 scaledSize =
                NCard.defaultSize * MgrVisualTuning.Performances.PreviewScale;
            Vector2 halfSize = scaledSize * 0.5f;
            Vector2 desiredCenter = new(
                mouse.X + MgrVisualTuning.Performances.PreviewMouseXOffset + halfSize.X,
                mouse.Y);

            Rect2 viewportRect = _anchor.GetViewport().GetVisibleRect();
            desiredCenter.X = Math.Clamp(
                desiredCenter.X,
                viewportRect.Position.X + halfSize.X + 8f,
                viewportRect.End.X - halfSize.X - 8f);
            desiredCenter.Y = Math.Clamp(
                desiredCenter.Y,
                viewportRect.Position.Y + halfSize.Y + 8f,
                viewportRect.End.Y - halfSize.Y - 8f);
            _hoverPreview.Position = desiredCenter;
        }

        private void PositionHoverHitbox()
        {
            if (!GodotObject.IsInstanceValid(_hoverHitbox))
                return;

            _hoverHitbox.SyncToTarget();
        }

        private void HideHoverPreview()
        {
            if (_hoverPreview is null)
                return;

            if (GodotObject.IsInstanceValid(_hoverPreview))
                _hoverPreview.QueueFreeSafely();

            _hoverPreview = null;
        }

        public void Dispose()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
            HideHoverPreview();

            if (GodotObject.IsInstanceValid(_hoverHitbox))
            {
                _hoverHitbox.MouseEntered -= OnMouseEntered;
                _hoverHitbox.MouseExited -= OnMouseExited;
                _hoverHitbox.GuiInput -= OnHoverInput;
                _hoverHitbox.QueueFree();
            }

            // NCard instances are pooled rather than destroyed. Custom children
            // must be detached before QueueFreeSafely returns the card to that
            // pool, otherwise the counter/glow reappear on whichever discard,
            // reward or preview card receives this NCard instance next.
            DetachAndFreeDecoration(_triggerGlow);
            DetachAndFreeDecoration(_idleEdge);

            if (GodotObject.IsInstanceValid(_cardNode))
                _cardNode.QueueFreeSafely();

            if (GodotObject.IsInstanceValid(_anchor))
                _anchor.QueueFree();
        }

        private static void DetachAndFreeDecoration(CanvasItem decoration)
        {
            if (!GodotObject.IsInstanceValid(decoration))
                return;

            Node? parent = decoration.GetParent();
            if (parent is not null && GodotObject.IsInstanceValid(parent))
                parent.RemoveChild(decoration);

            decoration.QueueFree();
        }
    }
}
