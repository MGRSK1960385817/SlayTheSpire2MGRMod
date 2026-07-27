using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

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
        MgrPerformanceEntry entry)
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
                QueueEntryAnimation(player, entry);
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
    public static void QueueEntryAnimation(Player player, MgrPerformanceEntry entry)
    {
        if (Racks.TryGetValue(player, out PerformanceRack? rack) && rack.IsValid)
            rack.QueuePlayedCardAnimation(entry);
    }

    public static Task PlayTriggerAnimation(Player player, MgrPerformanceEntry entry)
    {
        if (!Racks.TryGetValue(player, out PerformanceRack? rack) || !rack.IsValid)
            return Task.CompletedTask;

        return rack.PlayTriggerAnimation(entry);
    }

    public static Task PlayExitAnimation(
        Player player,
        MgrPerformanceEntry entry,
        PileType? destinationPile)
    {
        if (!Racks.TryGetValue(player, out PerformanceRack? rack) || !rack.IsValid)
            return Task.CompletedTask;

        return rack.PlayExitAnimation(entry, destinationPile);
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
        private readonly CanvasLayer _previewLayer;
        private readonly List<PerformanceCardView> _views = [];

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

            // CardPreviewContainer owns a layout script that moves every child
            // back to the screen centre. A private canvas layer lets the rack
            // keep hover previews beside the mouse and above combat UI.
            _previewLayer = new CanvasLayer
            {
                Name = "MgrPerformancePreviewLayer",
                Layer = 90
            };
            parent.AddChild(_previewLayer);
        }

        public void Show(IReadOnlyList<MgrPerformanceEntry> entries)
        {
            foreach (PerformanceCardView stale in _views
                         .Where(view => !entries.Any(
                             entry => ReferenceEquals(entry, view.Entry)))
                         .ToArray())
            {
                stale.Dispose();
                _views.Remove(stale);
            }

            float spacing = entries.Count <= 1
                ? 0f
                : MathF.Min(
                    MgrVisualTuning.Performances.DesiredSpacing,
                    MgrVisualTuning.Performances.MaximumWidth / (entries.Count - 1));
            float center = (entries.Count - 1) * 0.5f;
            var orderedViews = new List<PerformanceCardView>(entries.Count);

            for (int index = 0; index < entries.Count; index++)
            {
                // The first entry is the rightmost; newer entries extend left.
                float x = (center - index) * spacing;
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

        public Task PlayTriggerAnimation(MgrPerformanceEntry entry) =>
            FindView(entry)?.PlayTriggerAnimation() ?? Task.CompletedTask;

        public Task PlayExitAnimation(
            MgrPerformanceEntry entry,
            PileType? destinationPile)
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

            return view.PlayExitAnimation(destination, hasPileDestination);
        }

        public void QueuePlayedCardAnimation(MgrPerformanceEntry entry)
        {
            PerformanceCardView? destination = FindView(entry);
            if (destination is null)
                return;

            // Capture the slot now: the entry may complete and disappear while
            // the final autoplay view is still finishing its pile transition.
            TaskHelper.RunSafely(
                AwaitPlayedCardAndAnimate(entry.Card, destination.ViewportCenter));
        }

        private async Task AwaitPlayedCardAndAnimate(CardModel card, Vector2 destination)
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
                    AnimatePlayedCardTo(destination, playedCard);
                    return;
                }

                await _root.ToSignal(_root.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            GD.PushWarning($"MGR Performance could not find the played view for {card.GetType().Name}.");
        }

        private static void AnimatePlayedCardTo(
            Vector2 destinationInViewport,
            NCard playedCard)
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
            var tween = playedCard.CreateTween().SetParallel();
            tween.TweenProperty(
                    playedCard,
                    "global_position",
                    targetPosition,
                    MgrVisualTuning.Performances.EnterQueueSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                    playedCard,
                    "scale",
                    finalScale,
                    MgrVisualTuning.Performances.EnterQueueSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(
                playedCard,
                "modulate",
                new Color(1f, 1f, 1f, 0.12f),
                MgrVisualTuning.Performances.EnterQueueSeconds);
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
        private readonly Label _remainingLabel;
        private Tween? _pulseTween;
        private NCard? _hoverPreview;
        private int _baseLayer;

        public MgrPerformanceEntry Entry { get; }
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
            _cardNode.MouseFilter = Control.MouseFilterEnum.Ignore;

            Vector2 unscaledGlowMargin = new(
                11f / MiniatureScale.X,
                11f / MiniatureScale.Y);
            _triggerGlow = new ColorRect
            {
                Name = "TriggerGlow",
                Position = VisibleCardRect.Position - unscaledGlowMargin,
                Size = VisibleCardRect.Size + unscaledGlowMargin * 2f,
                Color = new Color("d73ee7"),
                Modulate = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = -1
            };
            // Body is the actual visible CardContainer. Making every overlay a
            // child of it means internal NCard offsets/animation can no longer
            // separate the card face from its glow or counter.
            _cardBody.AddChild(_triggerGlow);
            _cardBody.MoveChild(_triggerGlow, 0);

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

            _remainingLabel = new Label
            {
                Name = "RemainingPerformances",
                Text = entry.RemainingPerformanceTurns.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = MgrVisualTuning.Performances.RemainingLabelZIndex
            };

            // Tuning values are expressed in final screen pixels. The label is
            // now inside the miniature's scaled Body, so compensate once here.
            Vector2 unscaledLabelSize = new(
                MgrVisualTuning.Performances.RemainingLabelSize.X / MiniatureScale.X,
                MgrVisualTuning.Performances.RemainingLabelSize.Y / MiniatureScale.Y);
            Vector2 unscaledLabelInset = new(
                MgrVisualTuning.Performances.RemainingLabelBottomRightInset.X / MiniatureScale.X,
                MgrVisualTuning.Performances.RemainingLabelBottomRightInset.Y / MiniatureScale.Y);
            _remainingLabel.Position =
                VisibleCardRect.End - unscaledLabelInset - unscaledLabelSize;
            _remainingLabel.Size = unscaledLabelSize;
            _remainingLabel.AddThemeFontSizeOverride(
                "font_size",
                ScaleThemeValueForMiniature(
                    MgrVisualTuning.Performances.RemainingLabelFontSize));
            _remainingLabel.AddThemeColorOverride(
                "font_color",
                MgrVisualTuning.Performances.RemainingLabelColor);
            _remainingLabel.AddThemeColorOverride(
                "font_outline_color",
                MgrVisualTuning.Performances.RemainingLabelOutlineColor);
            _remainingLabel.AddThemeConstantOverride(
                "outline_size",
                ScaleThemeValueForMiniature(
                    MgrVisualTuning.Performances.RemainingLabelOutlineSize));
            _cardBody.AddChild(_remainingLabel);

            Refresh();
        }

        public void Refresh()
        {
            _remainingLabel.Text = Entry.RemainingPerformanceTurns.ToString();
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
        }

        public void SetLayer(int layer)
        {
            _baseLayer = layer;
            _anchor.ZIndex = layer;
            _hoverHitbox.ZIndex = layer;
        }

        public async Task PlayTriggerAnimation()
        {
            if (!GodotObject.IsInstanceValid(_anchor) || !_anchor.IsInsideTree())
                return;

            HideHoverPreview();
            _pulseTween?.Kill();
            _anchor.Scale = Vector2.One;
            _triggerGlow.Modulate = new Color(1f, 1f, 1f, 0f);

            Tween tween = _anchor.CreateTween();
            _pulseTween = tween;
            tween.TweenProperty(
                    _anchor,
                    "scale",
                    Vector2.One * MgrVisualTuning.Performances.TriggerScale,
                    MgrVisualTuning.Performances.TriggerGrowSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.Parallel().TweenProperty(
                _triggerGlow,
                "modulate",
                new Color(1f, 1f, 1f, 0.9f),
                MgrVisualTuning.Performances.TriggerGrowSeconds);
            tween.TweenProperty(
                    _anchor,
                    "scale",
                    Vector2.One,
                    MgrVisualTuning.Performances.TriggerSettleSeconds)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.Parallel().TweenProperty(
                _triggerGlow,
                "modulate",
                new Color(1f, 1f, 1f, 0f),
                MgrVisualTuning.Performances.TriggerSettleSeconds);

            bool completed = await TweenHelper.AwaitFinished(tween, _anchor);
            if (completed && ReferenceEquals(_pulseTween, tween))
                _pulseTween = null;
        }

        public async Task PlayExitAnimation(
            Vector2 destinationInViewport,
            bool hasPileDestination)
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

            Tween tween = _anchor.CreateTween().SetParallel();
            tween.TweenProperty(
                    _anchor,
                    "global_position",
                    destinationInRackCanvas,
                    MgrVisualTuning.Performances.ExitSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(
                    _anchor,
                    "scale",
                    hasPileDestination ? new Vector2(0.34f, 0.34f) : new Vector2(0.82f, 0.82f),
                    MgrVisualTuning.Performances.ExitSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(_anchor, "modulate", new Color(1f, 1f, 1f, 0f), 0.26)
                .SetDelay(0.12);

            await TweenHelper.AwaitFinished(tween, _anchor);
        }

        private void OnMouseEntered()
        {
            _anchor.ZIndex = 300;
            _hoverHitbox.ZIndex = 300;
            _cardNode.Scale = MgrVisualTuning.Performances.HoveredMiniatureScale;
            ShowHoverPreview();
        }

        private void OnMouseExited()
        {
            _anchor.ZIndex = _baseLayer;
            _hoverHitbox.ZIndex = _baseLayer;
            _cardNode.Scale = MiniatureScale;
            HideHoverPreview();
        }

        private void OnHoverInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseMotion)
                PositionHoverPreview();
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

        private static int ScaleThemeValueForMiniature(int value)
        {
            float scale = MathF.Max(0.01f, MiniatureScale.X);
            return Math.Max(1, Mathf.RoundToInt(value / scale));
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
            DetachAndFreeDecoration(_remainingLabel);
            DetachAndFreeDecoration(_triggerGlow);

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
