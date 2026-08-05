using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// A code-drawn music staff behind the performance queue. This node is kept
/// completely separate from card layout and input so it cannot affect rack
/// positions, hover hitboxes, or play/exit routing.
/// </summary>
internal sealed partial class MgrPerformanceStaffVisual : Node2D
{
    private sealed class DriftingMarker
    {
        public MgrMusicSymbol Symbol;
        public int LineIndex;
        public float X;
        public float Speed;
        public float Radius;
        public float BobPhase;
        public float BobSpeed;
        public float TintMix;

        public float HalfWidth =>
            MgrMusicGlyphRenderer.GetHalfWidth(Symbol, Radius);

        public bool SpansTwoLines =>
            MgrMusicGlyphRenderer.SpansTwoStaffLines(Symbol);

        public bool OccupiesLine(int lineIndex) =>
            lineIndex == LineIndex ||
            SpansTwoLines && lineIndex == LineIndex + 1;
    }

    private readonly List<DriftingMarker> _markers = [];
    private float[] _lastMarkerSpawnTimes = [];
    private float[] _lineSpawnCooldownUntilTimes = [];
    private double _spawnSeconds;
    private float _glowAmount;
    private float _triggerPulse;
    private float _animationTime;
    private bool _isPerforming;
    private int _performanceCardsPlayedThisCombat;
    private MgrPerformanceStaffMarkerClipVisual? _markerClip;
    private Node2D? _playheadRoot;
    private Tween? _playheadTween;

    public override void _Ready()
    {
        Position = MgrVisualTuning.Performances.StaffOffset;
        ZIndex = MgrVisualTuning.Performances.StaffZIndex;
        CreateMarkerClip();
        CreatePlayhead();
        ResetSpawnTimer();
        QueueRedraw();
        _markerClip?.QueueMarkerRedraw();
    }

    public override void _Process(double delta)
    {
        float elapsed = (float)delta;
        float flowSpeed = _isPerforming
            ? MathF.Max(
                1f,
                MgrVisualTuning.Performances.StaffPerformingFlowSpeedMultiplier)
            : 1f;
        float flowElapsed = elapsed * flowSpeed;
        _animationTime += flowElapsed;
        float rightEdge = MgrVisualTuning.Performances.StaffWidth * 0.5f;

        for (int index = _markers.Count - 1; index >= 0; index--)
        {
            DriftingMarker marker = _markers[index];
            marker.X += marker.Speed * flowElapsed;
            marker.BobPhase += marker.BobSpeed * flowElapsed;
            if (marker.X - marker.HalfWidth > rightEdge)
                _markers.RemoveAt(index);
        }

        _spawnSeconds -= delta * flowSpeed;
        if (_spawnSeconds <= 0.0)
        {
            int maximumMarkers = GetMaximumMarkerCount();
            bool spawned = _markers.Count < maximumMarkers &&
                TrySpawnMarker();
            if (spawned || _markers.Count >= maximumMarkers)
            {
                ResetSpawnTimer();
            }
            else
            {
                _spawnSeconds = GetSpawnRetrySeconds();
            }
        }

        float targetGlow = _isPerforming ? 1f : 0f;
        float fadeSeconds = MathF.Max(
            0.001f,
            (float)MgrVisualTuning.Performances.StaffGlowFadeSeconds);
        _glowAmount = Mathf.MoveToward(
            _glowAmount,
            targetGlow,
            elapsed / fadeSeconds);
        _triggerPulse = Mathf.MoveToward(
            _triggerPulse,
            0f,
            elapsed / MathF.Max(
                0.001f,
                (float)MgrVisualTuning.Performances.StaffTriggerPulseSeconds));

        QueueRedraw();
        _markerClip?.QueueMarkerRedraw();
    }

    public override void _Draw()
    {
        int lineCount = Math.Max(1, MgrVisualTuning.Performances.StaffLineCount);
        float halfWidth = MgrVisualTuning.Performances.StaffWidth * 0.5f;
        float top = -(lineCount - 1) * MgrVisualTuning.Performances.StaffLineSpacing * 0.5f;
        float flash = Mathf.SmoothStep(0f, 1f, _glowAmount);
        float impact = Mathf.SmoothStep(0f, 1f, _triggerPulse);

        Color lineColor = MgrVisualTuning.Performances.StaffLineColor.Lerp(
            MgrVisualTuning.Performances.StaffFlashColor,
            flash);
        lineColor.A = Mathf.Lerp(
            MgrVisualTuning.Performances.StaffLineAlpha,
            0.92f,
            flash);

        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            float y = top + lineIndex * MgrVisualTuning.Performances.StaffLineSpacing;
            List<DriftingMarker> lineMarkers = _markers
                .Where(marker => marker.OccupiesLine(lineIndex))
                .OrderBy(marker => marker.X)
                .ToList();

            float segmentStart = -halfWidth;
            foreach (DriftingMarker marker in lineMarkers)
            {
                float gap = marker.HalfWidth +
                    MgrVisualTuning.Performances.StaffMarkerGapPadding;
                float segmentEnd = Math.Clamp(marker.X - gap, -halfWidth, halfWidth);
                DrawStaffSegment(segmentStart, segmentEnd, y, lineColor, flash, impact);
                segmentStart = Math.Clamp(marker.X + gap, -halfWidth, halfWidth);
            }

            DrawStaffSegment(segmentStart, halfWidth, y, lineColor, flash, impact);
        }

        DrawStaffEndOrnaments(
            halfWidth,
            top,
            lineCount,
            lineColor,
            flash,
            impact);

        if (flash > 0.01f || impact > 0.01f)
            DrawPerformanceSparkles(halfWidth, flash, impact);

    }

    public void SetActive(bool active)
    {
        bool wasActive = Visible && IsProcessing();
        Visible = active;
        SetProcess(active);
        if (active)
        {
            if (!wasActive && _markers.Count == 0)
            {
                int initialCount = Math.Min(
                    MgrVisualTuning.Performances.StaffInitialMarkerCount,
                    GetMaximumMarkerCount());
                for (int index = 0; index < initialCount; index++)
                {
                    int lineCount = Math.Max(
                        1,
                        MgrVisualTuning.Performances.StaffLineCount);
                    MgrMusicSymbol symbol = SelectRandomMusicSymbol();
                    int maximumStartLine = symbol == MgrMusicSymbol.TwoLineChord
                        ? Math.Max(0, lineCount - 2)
                        : lineCount - 1;
                    SpawnMarker(
                        lineIndex: Math.Min(index * 2 % lineCount, maximumStartLine),
                        startInsideStaff: true,
                        requestedSymbol: symbol);
                }
            }

            if (_spawnSeconds <= 0.0)
                ResetSpawnTimer();
        }
        else
        {
            _markers.Clear();
            _isPerforming = false;
            _glowAmount = 0f;
            _triggerPulse = 0f;
            _playheadTween?.Kill();
            _playheadTween = null;
            if (_playheadRoot is not null &&
                GodotObject.IsInstanceValid(_playheadRoot))
            {
                _playheadRoot.Visible = false;
            }
        }

        QueueRedraw();
    }

    public void SetPerforming(bool isPerforming)
    {
        if (!Visible)
            return;

        if (_isPerforming == isPerforming)
            return;

        _isPerforming = isPerforming;
        // Entering a performance keeps the same ambient simulation but advances
        // it at the global flow multiplier. Clamp the first pending spawn so the
        // fast-forward becomes visible promptly. Leaving starts a fresh idle
        // interval instead of carrying a partially accelerated timer across.
        if (isPerforming)
        {
            _spawnSeconds = Math.Min(
                _spawnSeconds,
                MgrVisualTuning.Performances.StaffMarkerSpawnMinSeconds);
        }
        else
        {
            ResetSpawnTimer();
        }
        QueueRedraw();
    }

    public void SetPerformanceCardsPlayedThisCombat(int cardsPlayed)
    {
        int normalized = Math.Max(0, cardsPlayed);
        if (_performanceCardsPlayedThisCombat == normalized)
            return;

        float oldMultiplier = GetCombatSpawnFrequencyMultiplier();
        _performanceCardsPlayedThisCombat = normalized;
        float newMultiplier = GetCombatSpawnFrequencyMultiplier();
        if (_spawnSeconds > 0.0 && newMultiplier > oldMultiplier)
            _spawnSeconds *= oldMultiplier / newMultiplier;
    }

    public void Pulse()
    {
        if (!Visible)
            return;

        _triggerPulse = 1f;
        QueueRedraw();
    }

    private void DrawStaffSegment(
        float fromX,
        float toX,
        float y,
        Color color,
        float flash,
        float impact)
    {
        if (toX <= fromX)
            return;

        if (flash > 0f || impact > 0f)
        {
            Color glow = MgrVisualTuning.Performances.StaffFlashColor;
            glow.A = 0.08f * flash + 0.13f * impact;
            DrawLine(
                new Vector2(fromX, y),
                new Vector2(toX, y),
                glow,
                MgrVisualTuning.Performances.StaffLineThickness +
                    MgrVisualTuning.Performances.StaffFlashGlowWidth,
                antialiased: true);

            DrawStaffSweeps(fromX, toX, y, flash, impact);
        }

        DrawLine(
            new Vector2(fromX, y),
            new Vector2(toX, y),
            color,
            MgrVisualTuning.Performances.StaffLineThickness + flash + impact * 0.7f,
            antialiased: true);
    }

    private void DrawStaffEndOrnaments(
        float halfWidth,
        float top,
        int lineCount,
        Color lineColor,
        float flash,
        float impact)
    {
        float bottom = top +
            (lineCount - 1) * MgrVisualTuning.Performances.StaffLineSpacing;
        DrawStaffEndOrnament(
            -halfWidth,
            -1f,
            top,
            bottom,
            lineColor,
            flash,
            impact);
        DrawStaffEndOrnament(
            halfWidth,
            1f,
            top,
            bottom,
            lineColor,
            flash,
            impact);
    }

    private void DrawStaffEndOrnament(
        float edgeX,
        float outwardDirection,
        float top,
        float bottom,
        Color lineColor,
        float flash,
        float impact)
    {
        Color barColor = lineColor.Lerp(
            MgrVisualTuning.Performances.StaffFlashColor,
            flash * 0.48f + impact * 0.30f);
        barColor.A = Math.Clamp(
            MgrVisualTuning.Performances.StaffEndBarAlpha +
                flash * 0.22f + impact * 0.18f,
            0f,
            1f);

        // A thin/thick double barline keeps the boundary readable while the
        // surrounding stars provide the softer animated finish.
        DrawLine(
            new Vector2(edgeX, top),
            new Vector2(edgeX, bottom),
            barColor,
            MgrVisualTuning.Performances.StaffEndBarThinWidth,
            antialiased: true);
        float thickBarX = edgeX + outwardDirection *
            MgrVisualTuning.Performances.StaffEndBarSeparation;
        DrawLine(
            new Vector2(thickBarX, top),
            new Vector2(thickBarX, bottom),
            barColor,
            MgrVisualTuning.Performances.StaffEndBarThickWidth,
            antialiased: true);

        if (flash > 0.01f || impact > 0.01f)
        {
            DrawStaffEndStarSpray(
                edgeX,
                outwardDirection,
                top,
                bottom,
                flash,
                impact);
        }
    }

    private void DrawStaffEndStarSpray(
        float edgeX,
        float outwardDirection,
        float top,
        float bottom,
        float flash,
        float impact)
    {
        int count = Math.Max(
            1,
            MgrVisualTuning.Performances.StaffEndSprayStarCount);
        float middle = (top + bottom) * 0.5f;
        int sideColorOffset = outwardDirection > 0f ? 2 : 5;
        for (int index = 0; index < count; index++)
        {
            float rawPhase = _animationTime *
                MgrVisualTuning.Performances.StaffEndSpraySpeed +
                index / (float)count +
                (outwardDirection > 0f ? 0.41f : 0f);
            float progress = Mathf.PosMod(rawPhase, 1f);
            float eased = 1f - MathF.Pow(1f - progress, 2.4f);
            float verticalSeed = MathF.Sin(
                index * 12.9898f + sideColorOffset * 2.37f);
            float distance = Mathf.Lerp(
                MgrVisualTuning.Performances.StaffEndSprayStartDistance,
                MgrVisualTuning.Performances.StaffEndSprayEndDistance,
                eased);
            var center = new Vector2(
                edgeX + outwardDirection * distance,
                middle + verticalSeed *
                    MgrVisualTuning.Performances.StaffEndSprayVerticalSpread +
                MathF.Sin(progress * MathF.PI) * verticalSeed * 9f);
            float fade = MathF.Pow(1f - progress, 1.15f);
            // Every stream advances through the palette on each emission
            // cycle, so a fixed position no longer repeats one fixed color.
            int colorCycle = Mathf.FloorToInt(rawPhase);
            Color color = GetSparkleColor(
                index * 3 + colorCycle * 5 + sideColorOffset);
            color.A = Math.Clamp(
                fade * (0.44f * flash + 0.42f * impact),
                0f,
                1f);

            float streakLength = (5f + 8f * fade) *
                MgrVisualTuning.Performances.StaffEndSprayStreakScale;
            DrawLine(
                center - new Vector2(outwardDirection * streakLength, 0f),
                center + new Vector2(outwardDirection * 1.5f, 0f),
                color,
                1.1f + fade * 1.2f,
                antialiased: true);
            DrawGlowingStar(
                center,
                MgrVisualTuning.Performances.StaffEndSprayStarRadius *
                    (0.72f + fade * 0.58f),
                color);
        }
    }

    private void DrawGlowingStar(Vector2 center, float radius, Color color)
    {
        Color halo = color;
        halo.A *= 0.24f;
        DrawCircle(center, radius * 2.5f, halo);
        DrawLine(
            center + new Vector2(-radius, 0f),
            center + new Vector2(radius, 0f),
            color,
            1.25f,
            antialiased: true);
        DrawLine(
            center + new Vector2(0f, -radius * 1.35f),
            center + new Vector2(0f, radius * 1.35f),
            color,
            1.25f,
            antialiased: true);
    }

    private void CreateMarkerClip()
    {
        _markerClip = new MgrPerformanceStaffMarkerClipVisual
        {
            Name = "StaffMarkerClip",
            ZIndex = 1
        };
        _markerClip.Initialize(DrawMarkers);
        AddChild(_markerClip);
    }

    private void DrawMarkers(Node2D canvas)
    {
        int lineCount = Math.Max(1, MgrVisualTuning.Performances.StaffLineCount);
        float top = -(lineCount - 1) *
            MgrVisualTuning.Performances.StaffLineSpacing * 0.5f;
        float flash = Mathf.SmoothStep(0f, 1f, _glowAmount);
        float impact = Mathf.SmoothStep(0f, 1f, _triggerPulse);
        Color markerColor = MgrVisualTuning.Performances.StaffMarkerColor.Lerp(
            MgrVisualTuning.Performances.StaffFlashColor,
            flash);
        markerColor.A = Mathf.Lerp(
            MgrVisualTuning.Performances.StaffMarkerAlpha,
            1f,
            flash);

        foreach (DriftingMarker marker in _markers)
        {
            float y = top + marker.LineIndex *
                MgrVisualTuning.Performances.StaffLineSpacing;
            if (marker.SpansTwoLines)
                y += MgrVisualTuning.Performances.StaffLineSpacing * 0.5f;
            y += MathF.Sin(marker.BobPhase) *
                MgrVisualTuning.Performances.StaffMarkerBobAmplitude;
            var center = new Vector2(marker.X, y);

            float symbolPulse = 1f + impact * 0.22f;
            Color symbolColor = markerColor.Lerp(
                new Color("8de5ff"),
                marker.TintMix * 0.42f);
            symbolColor.A = markerColor.A;

            MgrMusicGlyphRenderer.Draw(
                canvas,
                marker.Symbol,
                center,
                marker.Radius * symbolPulse,
                symbolColor,
                1.7f + flash + impact,
                MgrVisualTuning.Performances.StaffLineSpacing);
        }
    }

    private void DrawStaffSweeps(
        float fromX,
        float toX,
        float y,
        float flash,
        float impact)
    {
        float width = MgrVisualTuning.Performances.StaffWidth;
        float halfWidth = width * 0.5f;
        int sweepCount = Math.Max(1, MgrVisualTuning.Performances.StaffSweepCount);
        for (int index = 0; index < sweepCount; index++)
        {
            float wrapped = Mathf.PosMod(
                _animationTime * MgrVisualTuning.Performances.StaffSweepSpeed +
                width * index / sweepCount,
                width);
            float center = -halfWidth + wrapped;
            float sweepStart = MathF.Max(
                fromX,
                center - MgrVisualTuning.Performances.StaffSweepHalfWidth);
            float sweepEnd = MathF.Min(
                toX,
                center + MgrVisualTuning.Performances.StaffSweepHalfWidth);
            if (sweepEnd <= sweepStart)
                continue;

            float segmentCenter = (sweepStart + sweepEnd) * 0.5f;
            float distance = MathF.Abs(segmentCenter - center) /
                MgrVisualTuning.Performances.StaffSweepHalfWidth;
            Color sweep = new("b9ecff");
            sweep.A = (0.27f * flash + 0.36f * impact) *
                MathF.Max(0f, 1f - distance);
            DrawLine(
                new Vector2(sweepStart, y),
                new Vector2(sweepEnd, y),
                sweep,
                MgrVisualTuning.Performances.StaffLineThickness + 3.8f,
                antialiased: true);
        }
    }

    private void DrawPerformanceSparkles(
        float halfWidth,
        float flash,
        float impact)
    {
        int count = Math.Max(0, MgrVisualTuning.Performances.StaffSparkleCount);
        for (int index = 0; index < count; index++)
        {
            float seed = index * 17.37f;
            float x = -halfWidth + Mathf.PosMod(
                seed * 23f + _animationTime * (31f + index % 3 * 8f),
                halfWidth * 2f);
            float y = -MgrVisualTuning.Performances.StaffSparkleVerticalHalfExtent +
                Mathf.PosMod(
                    seed * 11f,
                    MgrVisualTuning.Performances.StaffSparkleVerticalHalfExtent * 2f);
            float flicker = 0.5f + 0.5f * MathF.Sin(
                _animationTime * (4.2f + index % 4) + seed);
            float alpha = (0.31f * flash + 0.62f * impact) * flicker;
            if (alpha < 0.02f)
                continue;

            Color sparkle = GetSparkleColor(index);
            sparkle.A = alpha;
            float radius = MgrVisualTuning.Performances.StaffSparkleRadius *
                (0.65f + flicker * 0.7f + impact * 0.45f);
            DrawLine(
                new Vector2(x - radius * 2f, y),
                new Vector2(x + radius * 2f, y),
                sparkle,
                1.2f,
                antialiased: true);
            DrawLine(
                new Vector2(x, y - radius * 2f),
                new Vector2(x, y + radius * 2f),
                sparkle,
                1.2f,
                antialiased: true);
        }

    }

    public async Task PrepareTrigger(float targetX, float durationScale)
    {
        if (!Visible || _playheadRoot is null ||
            !GodotObject.IsInstanceValid(_playheadRoot))
        {
            return;
        }

        _playheadTween?.Kill();
        float halfWidth = MgrVisualTuning.Performances.StaffWidth * 0.5f;
        _playheadRoot.Visible = true;
        _playheadRoot.Position = new Vector2(
            -halfWidth - MgrVisualTuning.Performances.StaffPlayheadEdgePadding,
            0f);
        _playheadRoot.Scale = new Vector2(1f, 0.42f);
        _playheadRoot.Modulate = new Color(1f, 1f, 1f, 0f);
        double approachSeconds =
            MgrVisualTuning.Performances.StaffPlayheadApproachSeconds *
            Math.Clamp(durationScale, 0.1f, 1f);

        Tween tween = CreateTween().SetParallel();
        _playheadTween = tween;
        tween.TweenProperty(
                _playheadRoot,
                "position:x",
                targetX,
                approachSeconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(
                _playheadRoot,
                "scale",
                Vector2.One,
                approachSeconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(
            _playheadRoot,
            "modulate",
            Colors.White,
            approachSeconds);

        bool completed = await TweenHelper.AwaitFinished(tween, this);
        if (completed && ReferenceEquals(_playheadTween, tween))
            _playheadTween = null;
    }

    public async Task CompleteTrigger(float durationScale)
    {
        if (_playheadRoot is null ||
            !GodotObject.IsInstanceValid(_playheadRoot) ||
            !_playheadRoot.Visible)
        {
            return;
        }

        _playheadTween?.Kill();
        float halfWidth = MgrVisualTuning.Performances.StaffWidth * 0.5f;
        double departureSeconds =
            MgrVisualTuning.Performances.StaffPlayheadDepartureSeconds *
            Math.Clamp(durationScale, 0.1f, 1f);
        Tween tween = CreateTween().SetParallel();
        _playheadTween = tween;
        tween.TweenProperty(
                _playheadRoot,
                "position:x",
                halfWidth + MgrVisualTuning.Performances.StaffPlayheadEdgePadding,
                departureSeconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(
            _playheadRoot,
            "modulate",
            new Color(1f, 1f, 1f, 0f),
            departureSeconds);

        bool completed = await TweenHelper.AwaitFinished(tween, this);
        if (completed && ReferenceEquals(_playheadTween, tween))
        {
            _playheadTween = null;
            _playheadRoot.Visible = false;
        }
    }

    private bool TrySpawnMarker()
    {
        int lineCount = Math.Max(1, MgrVisualTuning.Performances.StaffLineCount);
        EnsureLineSpawnTimes(lineCount);
        MgrMusicSymbol symbol = SelectRandomMusicSymbol();
        int occupiedLineCount = MgrMusicGlyphRenderer.SpansTwoStaffLines(symbol)
            ? Math.Min(2, lineCount)
            : 1;
        int maximumStartLine = lineCount - occupiedLineCount;
        var candidates = new List<int>(maximumStartLine + 1);
        for (int lineIndex = 0; lineIndex <= maximumStartLine; lineIndex++)
        {
            if (CanSpawnAt(lineIndex, occupiedLineCount, lineCount))
                candidates.Add(lineIndex);
        }

        if (candidates.Count == 0)
            return false;

        SpawnMarker(
            candidates[Random.Shared.Next(candidates.Count)],
            startInsideStaff: false,
            requestedSymbol: symbol);
        return true;
    }

    private bool CanSpawnAt(
        int firstLine,
        int occupiedLineCount,
        int totalLineCount)
    {
        int lastLine = firstLine + occupiedLineCount - 1;
        for (int line = firstLine; line <= lastLine; line++)
        {
            if (_animationTime < _lineSpawnCooldownUntilTimes[line])
                return false;
        }

        for (int adjacent = Math.Max(0, firstLine - 1);
             adjacent <= Math.Min(totalLineCount - 1, lastLine + 1);
             adjacent++)
        {
            if (adjacent >= firstLine && adjacent <= lastLine)
                continue;

            if (_animationTime - _lastMarkerSpawnTimes[adjacent] <
                MgrVisualTuning.Performances.StaffAdjacentLineSpawnCooldownSeconds)
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnMarker(
        int lineIndex,
        bool startInsideStaff = false,
        MgrMusicSymbol? requestedSymbol = null)
    {
        int lineCount = Math.Max(1, MgrVisualTuning.Performances.StaffLineCount);
        EnsureLineSpawnTimes(lineCount);
        MgrMusicSymbol symbol = requestedSymbol ?? SelectRandomMusicSymbol();
        int occupiedLineCount = MgrMusicGlyphRenderer.SpansTwoStaffLines(symbol)
            ? Math.Min(2, lineCount)
            : 1;
        lineIndex = Math.Clamp(lineIndex, 0, lineCount - occupiedLineCount);
        float halfWidth = MgrVisualTuning.Performances.StaffWidth * 0.5f;
        float radius = RandomRange(
            MgrVisualTuning.Performances.StaffMarkerRadiusMin,
            MgrVisualTuning.Performances.StaffMarkerRadiusMax);
        float markerHalfWidth =
            MgrMusicGlyphRenderer.GetHalfWidth(symbol, radius);
        _markers.Add(new DriftingMarker
        {
            Symbol = symbol,
            LineIndex = lineIndex,
            X = startInsideStaff
                ? RandomRange(-halfWidth * 0.82f, halfWidth * 0.72f)
                : -halfWidth - markerHalfWidth -
                    MgrVisualTuning.Performances.StaffMarkerGapPadding,
            Speed = RandomRange(
                MgrVisualTuning.Performances.StaffMarkerSpeedMin,
                MgrVisualTuning.Performances.StaffMarkerSpeedMax),
            Radius = radius,
            BobPhase = RandomRange(0f, Mathf.Tau),
            BobSpeed = RandomRange(
                MgrVisualTuning.Performances.StaffMarkerBobSpeedMin,
                MgrVisualTuning.Performances.StaffMarkerBobSpeedMax),
            TintMix = Random.Shared.NextSingle()
        });
        float sameLineCooldown = RandomRange(
            MgrVisualTuning.Performances.StaffSameLineSpawnCooldownMinSeconds,
            MgrVisualTuning.Performances.StaffSameLineSpawnCooldownMaxSeconds);
        _lineSpawnCooldownUntilTimes[lineIndex] = MathF.Max(
            _lineSpawnCooldownUntilTimes[lineIndex],
            _animationTime + sameLineCooldown);
        _lastMarkerSpawnTimes[lineIndex] = _animationTime;

        // A glyph such as TwoLineChord interrupts two staff lines. Its anchor
        // line gets the normal randomized cooldown, while the crossed line gets
        // only a short reservation: enough to avoid visual stacking without
        // leaving a conspicuous empty lane.
        for (int line = lineIndex + 1;
             line < lineIndex + occupiedLineCount;
             line++)
        {
            _lastMarkerSpawnTimes[line] = _animationTime;
            float crossLineCooldown = RandomRange(
                MgrVisualTuning.Performances.StaffCrossLineSpawnCooldownMinSeconds,
                MgrVisualTuning.Performances.StaffCrossLineSpawnCooldownMaxSeconds);
            _lineSpawnCooldownUntilTimes[line] = MathF.Max(
                _lineSpawnCooldownUntilTimes[line],
                _animationTime + crossLineCooldown);
        }
    }

    private static MgrMusicSymbol SelectRandomMusicSymbol()
    {
        // Keep the staff readable: simple single notes dominate, pairs are
        // occasional, and progressively wider/denser glyphs are progressively
        // rarer. These are relative weights, not percentages.
        ReadOnlySpan<(MgrMusicSymbol Symbol, float Weight)> weights =
        [
            (MgrMusicSymbol.QuarterNote, 3.0f),
            (MgrMusicSymbol.EighthNote, 7.8f),
            (MgrMusicSymbol.SixteenthNote, 0.8f),
            (MgrMusicSymbol.HalfNote, 5.2f),
            (MgrMusicSymbol.BeamedPair, 3.7f),
            (MgrMusicSymbol.TwoLineChord, 2.1f),
            (MgrMusicSymbol.BeamedTriplet, 2.3f),
            (MgrMusicSymbol.BeamedQuartet, 1.4f)
        ];
        float total = 0f;
        foreach ((_, float weight) in weights)
            total += weight;

        float roll = Random.Shared.NextSingle() * total;
        foreach ((MgrMusicSymbol symbol, float weight) in weights)
        {
            roll -= weight;
            if (roll <= 0f)
                return symbol;
        }

        return MgrMusicSymbol.TwoLineChord;
    }

    private void EnsureLineSpawnTimes(int lineCount)
    {
        if (_lastMarkerSpawnTimes.Length == lineCount)
            return;

        _lastMarkerSpawnTimes = Enumerable.Repeat(-1000f, lineCount).ToArray();
        _lineSpawnCooldownUntilTimes = new float[lineCount];
    }

    private void ResetSpawnTimer()
    {
        float minimum =
            (float)MgrVisualTuning.Performances.StaffMarkerSpawnMinSeconds;
        float maximum =
            (float)MgrVisualTuning.Performances.StaffMarkerSpawnMaxSeconds;
        _spawnSeconds = RandomRange(minimum, maximum) /
            GetCombatSpawnFrequencyMultiplier();
    }

    private int GetMaximumMarkerCount()
    {
        int baseMaximum = _isPerforming
            ? MgrVisualTuning.Performances.StaffPerformingMaximumMarkers
            : MgrVisualTuning.Performances.StaffIdleMaximumMarkers;
        int additional = Math.Min(
            MgrVisualTuning.Performances.StaffMaximumAdditionalMarkersFromCombat,
            _performanceCardsPlayedThisCombat /
                Math.Max(
                    1,
                    MgrVisualTuning.Performances.StaffPerformanceCardsPerAdditionalMarker));
        return baseMaximum + additional;
    }

    private float GetCombatSpawnFrequencyMultiplier() =>
        MathF.Min(
            MgrVisualTuning.Performances.StaffMaximumCombatSpawnFrequencyMultiplier,
            1f + _performanceCardsPlayedThisCombat *
                MgrVisualTuning.Performances.StaffSpawnFrequencyIncreasePerPerformanceCard);

    private static double GetSpawnRetrySeconds() =>
        MgrVisualTuning.Performances.StaffMarkerSpawnRetrySeconds;

    private static float RandomRange(float minimum, float maximum) =>
        minimum + Random.Shared.NextSingle() * (maximum - minimum);

    private void CreatePlayhead()
    {
        float halfHeight =
            MgrVisualTuning.Performances.StaffPlayheadHeight * 0.5f;
        _playheadRoot = new Node2D
        {
            Name = "CurrentPerformancePlayhead",
            Visible = false,
            ZIndex = 8
        };
        AddChild(_playheadRoot);

        _playheadRoot.AddChild(new Line2D
        {
            Name = "Glow",
            Points = [new Vector2(0f, -halfHeight), new Vector2(0f, halfHeight)],
            Width = MgrVisualTuning.Performances.StaffPlayheadGlowWidth,
            DefaultColor = new Color(0.73f, 0.91f, 1f, 0.24f),
            Antialiased = true,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round
        });
        _playheadRoot.AddChild(new Line2D
        {
            Name = "Core",
            Points = [new Vector2(0f, -halfHeight), new Vector2(0f, halfHeight)],
            Width = MgrVisualTuning.Performances.StaffPlayheadCoreWidth,
            DefaultColor = new Color("fff2b8"),
            Antialiased = true,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round
        });
    }

    private static Color GetSparkleColor(int index) => (index % 6) switch
    {
        0 => new Color("fff0a8"),
        1 => new Color("b9eaff"),
        2 => new Color("efc2ff"),
        3 => new Color("bfffd3"),
        4 => new Color("ffd0df"),
        _ => Colors.White
    };
}
