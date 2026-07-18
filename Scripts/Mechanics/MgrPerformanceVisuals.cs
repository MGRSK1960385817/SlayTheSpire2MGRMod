using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
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

    public static void Show(Player player, IReadOnlyList<MgrPerformanceEntry> entries)
    {
        PerformanceRack? rack = GetOrCreateRack(player);
        rack?.Show(entries);
    }

    /// <summary>
    /// Starts immediately after the resolved CardPlay has been accepted into the
    /// Performance queue. This deliberately does not depend on CardModel.Played:
    /// that event and the Play-container view do not have identical timing for
    /// vanilla, colorless and modded cards.
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
        }

        public void Show(IReadOnlyList<MgrPerformanceEntry> entries)
        {
            ClearViews();
            if (entries.Count == 0)
                return;

            float spacing = entries.Count <= 1
                ? 0f
                : MathF.Min(
                    MgrVisualTuning.Performances.DesiredSpacing,
                    MgrVisualTuning.Performances.MaximumWidth / (entries.Count - 1));
            float center = (entries.Count - 1) * 0.5f;

            for (int index = 0; index < entries.Count; index++)
            {
                // The first entry is the rightmost; newer entries extend left.
                float x = (center - index) * spacing;
                var view = new PerformanceCardView(_root, entries[index]);
                view.SetPosition(new Vector2(x, 0f));
                // The earliest card is the rightmost and visually sits above
                // later cards where their bodies overlap.
                view.SetLayer(entries.Count - index);
                _views.Add(view);
            }
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

            Vector2 destination = view.GlobalCenter + new Vector2(0f, -100f);
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
                    destination = pile.GlobalPosition + pile.Size * 0.5f;
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
            _ = AwaitPlayedCardAndAnimate(entry.Card, destination.GlobalCenter);
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

        private static void AnimatePlayedCardTo(Vector2 destination, NCard playedCard)
        {
            if (!GodotObject.IsInstanceValid(playedCard))
                return;

            playedCard.PlayPileTween?.Kill();
            playedCard.MouseFilter = Control.MouseFilterEnum.Ignore;
            playedCard.ZIndex = 250;

            Vector2 finalScale = PerformanceCardView.MiniatureScale;
            Vector2 targetPosition = destination - playedCard.Size * finalScale * 0.5f;
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
                    playedCard.QueueFree();
            }));
        }

        private PerformanceCardView? FindView(MgrPerformanceEntry entry) =>
            _views.FirstOrDefault(view => ReferenceEquals(view.Entry, entry));

        private NCard? FindPlayedCardNode(CardModel card)
        {
            if (NCombatRoom.Instance is not { } room)
                return null;

            return FindPlayedCardNodeRecursive(room, card);
        }

        private NCard? FindPlayedCardNodeRecursive(Node node, CardModel card)
        {
            if (node is NCard candidate &&
                ReferenceEquals(candidate.Model, card) &&
                !_root.IsAncestorOf(candidate))
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
        }
    }

    private sealed class PerformanceCardView : IDisposable
    {
        public static Vector2 MiniatureScale =>
            MgrVisualTuning.Performances.MiniatureScale;

        private readonly Node2D _anchor;
        private readonly NCard _cardNode;
        private readonly Control _hoverHitbox;
        private readonly ColorRect _triggerGlow;
        private Tween? _pulseTween;
        private NCard? _hoverPreview;
        private int _baseLayer;

        public MgrPerformanceEntry Entry { get; }
        public Vector2 GlobalCenter => _anchor.GlobalPosition;

        public PerformanceCardView(Node parent, MgrPerformanceEntry entry)
        {
            Entry = entry;
            _anchor = new Node2D { Name = $"Performance_{entry.Card.GetType().Name}" };
            parent.AddChild(_anchor);

            _cardNode = NCard.Create(entry.Card, ModelVisibility.Visible)
                ?? throw new InvalidOperationException("Unable to create an MGR Performance card node.");
            _cardNode.Name = "Card";
            _cardNode.PivotOffset = _cardNode.Size * 0.5f;
            _cardNode.Position = -_cardNode.Size * 0.5f;
            _cardNode.Scale = MiniatureScale;
            _cardNode.MouseFilter = Control.MouseFilterEnum.Ignore;
            _anchor.AddChild(_cardNode);

            Vector2 visualSize = _cardNode.Size * MiniatureScale;
            Vector2 halfVisualSize = visualSize * 0.5f;
            _triggerGlow = new ColorRect
            {
                Name = "TriggerGlow",
                Position = -halfVisualSize - new Vector2(11f, 11f),
                Size = visualSize + new Vector2(22f, 22f),
                Color = new Color("d73ee7"),
                Modulate = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = -1
            };
            _anchor.AddChild(_triggerGlow);
            _anchor.MoveChild(_triggerGlow, 0);

            _hoverHitbox = new Control
            {
                Name = "HoverHitbox",
                Position = -halfVisualSize,
                Size = visualSize,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                ZIndex = 20
            };
            _hoverHitbox.MouseEntered += OnMouseEntered;
            _hoverHitbox.MouseExited += OnMouseExited;
            _hoverHitbox.GuiInput += OnHoverInput;
            _anchor.AddChild(_hoverHitbox);

            var remainingLabel = new Label
            {
                Name = "RemainingPerformances",
                Text = entry.RemainingPerformanceTurns.ToString(),
                Position = new Vector2(-36f, halfVisualSize.Y - 28f),
                Size = new Vector2(72f, 48f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 25
            };
            remainingLabel.AddThemeFontSizeOverride("font_size", 32);
            remainingLabel.AddThemeColorOverride("font_color", Colors.White);
            remainingLabel.AddThemeColorOverride("font_outline_color", new Color("a915b8"));
            remainingLabel.AddThemeConstantOverride("outline_size", 8);
            _anchor.AddChild(remainingLabel);
        }

        public void SetPosition(Vector2 position)
        {
            _anchor.Position = position;
        }

        public void SetLayer(int layer)
        {
            _baseLayer = layer;
            _anchor.ZIndex = layer;
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

        public async Task PlayExitAnimation(Vector2 destination, bool hasPileDestination)
        {
            if (!GodotObject.IsInstanceValid(_anchor) || !_anchor.IsInsideTree())
                return;

            HideHoverPreview();
            _pulseTween?.Kill();
            _pulseTween = null;
            _hoverHitbox.MouseFilter = Control.MouseFilterEnum.Ignore;
            _anchor.ZIndex = 450;

            Tween tween = _anchor.CreateTween().SetParallel();
            tween.TweenProperty(
                    _anchor,
                    "global_position",
                    destination,
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
            _cardNode.Scale = MgrVisualTuning.Performances.HoveredMiniatureScale;
            ShowHoverPreview();
        }

        private void OnMouseExited()
        {
            _anchor.ZIndex = _baseLayer;
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
            Control? previewContainer = NCombatRoom.Instance?.Ui?.CardPreviewContainer;
            (previewContainer as Node ?? _anchor).AddChild(_hoverPreview);
            _hoverPreview.PivotOffset = _hoverPreview.Size * 0.5f;
            _hoverPreview.Scale = new Vector2(0.5f, 0.5f);
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

            Vector2 mouse = _anchor.GetGlobalMousePosition();
            Vector2 scaledSize =
                _hoverPreview.Size * MgrVisualTuning.Performances.PreviewScale;
            Vector2 desired = new(
                mouse.X + MgrVisualTuning.Performances.PreviewMouseXOffset,
                mouse.Y - scaledSize.Y * 0.5f);

            Rect2 viewportRect = _anchor.GetViewport().GetVisibleRect();
            desired.X = Math.Clamp(
                desired.X,
                viewportRect.Position.X + 8f,
                viewportRect.End.X - scaledSize.X - 8f);
            desired.Y = Math.Clamp(
                desired.Y,
                viewportRect.Position.Y + 8f,
                viewportRect.End.Y - scaledSize.Y - 8f);
            _hoverPreview.GlobalPosition = desired;
        }

        private void HideHoverPreview()
        {
            if (_hoverPreview is null)
                return;

            if (GodotObject.IsInstanceValid(_hoverPreview))
                _hoverPreview.QueueFree();

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
            }

            if (GodotObject.IsInstanceValid(_anchor))
                _anchor.QueueFree();
        }
    }
}
