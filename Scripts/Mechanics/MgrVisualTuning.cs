using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Central tuning values for MGR combat presentation.
/// Change these values to adjust layout and animation without touching the
/// underlying card or note mechanics.
/// </summary>
public static class MgrVisualTuning
{
    public static class Notes
    {
        public static readonly Vector2 RackOffset = new(0f, -350f);
        // Filled-note artwork is normalized against its source texture and then
        // sized as a proportion of the slot diameter. This keeps high-resolution
        // replacement art consistent without imposing a fixed pixel target.
        public const float ArtworkFillRatio = 1f;
        public static readonly Vector2 AmountLabelPosition = new(-36f, 21f);
        public static readonly Vector2 AmountLabelSize = new(72f, 36f);
        public const int AmountLabelFontSize = 24;
        public const int AmountLabelOutlineSize = 8;
        public static readonly Color CurseAccentColor = new("78101c");

        public const int RackZIndex = 50;
        public const float DesiredSlotSpacing = 96f;
        public const float MaximumRackWidth = 480f;
        public const float SlotRadius = 30f;
        public const int EmptySlotDashCount = 8;
        public const float EmptySlotDashFill = 0.48f;
        public const float EmptySlotDashWidth = 2.5f;

        // Empty-slot frames rotate and carry a fixed-color traveling highlight.
        // Wide speed ranges deliberately keep neighboring slots out of lockstep.
        public static readonly Color EmptySlotBaseColor = new("b8c2d6");
        public const float EmptySlotBaseAlpha = 0.36f;
        public const float EmptySlotHighlightAlpha = 0.96f;
        public const float EmptySlotHighlightWidthBoost = 1.9f;
        public const float EmptySlotRotationDegreesPerSecond = 18f;
        public const float EmptySlotRotationMultiplierMin = 0.35f;
        public const float EmptySlotRotationMultiplierMax = 1.90f;
        public const float EmptySlotHighlightAngularSpeedMin = 0.85f;
        public const float EmptySlotHighlightAngularSpeedMax = 3.65f;
        public const float EmptySlotGlowOrbitRadius = 32f;
        public const float EmptySlotGlowLeadDegrees = 36f;
        public const float EmptySlotGlowCoreRadius = 2.8f;
        public const float EmptySlotGlowHaloRadius = 9.5f;
        public const float EmptySlotGlowStarLength = 6.5f;
        public const float EmptySlotBreathAmplitude = 0.035f;
        public const float EmptySlotBreathSpeed = 1.25f;
        public const double EmptySlotCollapseSeconds = 0.24;
        public const double EmptySlotAppearSeconds = 0.34;
        public const float EmptySlotTransitionRotation = 2.35f;
        public const float EmptySlotAppearOvershootScale = 1.32f;

        // Repeated note/chord activity in one turn accelerates presentation, but
        // both paths retain a visible minimum duration.
        public const double FirstNoteEntranceSeconds = 0.28;
        public const double MinimumNoteEntranceSeconds = 0.10;
        public const double NoteEntranceAccelerationPerNote = 0.018;
        public const double FirstChordHoldSeconds = 0.42;
        public const double MinimumChordHoldSeconds = 0.12;
        public const double ChordHoldAccelerationPerChord = 0.075;
        public const int FastChordCommandThreshold = 2;

        // Newly generated notes pop in one after another. ChannelSingleNote awaits
        // this animation, so cards that create several notes naturally serialize.
        public const float EntranceStartScale = 0.28f;
        public const float EntranceOvershootScale = 1.18f;
        public const float EntranceGrowFraction = 0.62f;
        public const float EntranceStartYOffset = 18f;

        // Shared star/glow language for note generation, chord resolution and
        // the transition between a filled note and its empty slot.
        public const int EntranceBurstParticleCount = 7;
        public const int ChordBurstParticleCount = 20;
        public const int SlotTransitionBurstParticleCount = 11;
        public const double EntranceBurstSeconds = 0.30;
        public const double ChordBurstSeconds = 0.46;
        public const double SlotTransitionBurstSeconds = 0.36;
        public const float EntranceBurstEndRadius = 44f;
        public const float ChordBurstEndRadius = 82f;
        public const float SlotTransitionBurstEndRadius = 58f;
        public const float NoteBurstStartRadius = 10f;
        public const float NoteBurstStarSize = 3.2f;
        public const float ChordTriggerScale = 1.16f;
        public const double ChordTriggerGrowSeconds = 0.10;
        public const double ChordTriggerSettleSeconds = 0.16;

        // Each generated note samples a small visual-only variance around these
        // values. This deliberately uses chaotic randomness: it never affects
        // combat state or replay determinism.
        public const float BobAmplitude = 5f;
        public const float BobAngularSpeed = 1.75f;
        public const float BreathAmplitude = 0.055f;
        public const float BreathAngularSpeed = 2.05f;
        public const float PhaseStep = 0.72f;
        public const float BobSpeedVariance = 0.22f;
        public const float BreathSpeedVariance = 0.20f;
        public const float InitialScaleVariance = 0.07f;
        public const float PhaseVariance = 0.65f;

        // Omnia Notes cycle through all five basic Note silhouettes and
        // the Starry silhouette while a rainbow flows across the current shape.
        public const double OmniaNoteShapeSeconds = 0.30;
        public const float OmniaNoteRainbowSpeed = 0.22f;
        public const float OmniaNoteRainbowFrequency = 1.35f;
    }

    public static class Performances
    {
        public static readonly Vector2 RackOffset = new(60f, -420f); // 后面的数字绝对值越大 演奏堆越靠上
        // The staff is a presentation-only child of the rack. Its local offset
        // can be tuned without changing card positions, hover hitboxes, or the
        // coordinate chain used by performance animations.
        public static readonly Vector2 StaffOffset = new(0f, -16f);
        public static readonly Vector2 MiniatureScale = new(0.35f, 0.35f);
        public static readonly Vector2 HoveredMiniatureScale = new(0.5f, 0.5f);
        public static readonly Vector2 PreviewScale = new(0.8f, 0.8f);
        public static readonly Color StaffLineColor = new("9b87c7");
        public static readonly Color StaffMarkerColor = new("d8c8ff");
        public static readonly Color StaffFlashColor = new("fff2b8");
        public static readonly Color PerformanceAccentColor = new("fff2b8");

        public const int RackZIndex = 55;
        public const int StaffZIndex = -20;
        // Remaining Performance turns use a small floating beat marker above
        // the card rather than a purple badge on its lower-right corner.
        public static readonly Vector2 RemainingCounterSize = new(54f, 34f);
        public static readonly Color RemainingCounterColor = PerformanceAccentColor;
        public static readonly Color RemainingCounterOutlineColor = new("443552");
        public const int RemainingCounterZIndex = 34;
        public const int RemainingCounterFontSize = 26;
        public const int RemainingCounterOutlineSize = 5;
        public const float RemainingCounterTopGap = 9f;
        public const float RemainingCounterWingLength = 24f;
        public const float RemainingCounterSingleWingLengthScale = 0.76f;
        public const float RemainingCounterDoubleWingLengthScale = 0.88f;
        public const float RemainingCounterWingGap = 14f;
        public const float RemainingCounterWingSpacing = 5f;
        public const int RemainingCounterWingLineCount = 3;
        public const float RemainingCounterLineWidth = 1.6f;
        public const double RemainingCounterPulseSeconds = 0.30;
        public const float RemainingCounterChangeFraction = 0.36f;
        // With fewer than this many cards, the rack grows from its right edge
        // toward the left at a fixed, roomy spacing. At this count and above,
        // the rack switches to a centred, progressively compressed layout.
        public const int FilledRackCardThreshold = 5;
        public const float UnfilledCardSpacing = 82f;
        // The filled rack widens only a little as more cards enter. Because the
        // added width per card is much smaller than UnfilledCardSpacing, the
        // visible overlap becomes progressively tighter.
        public const float FilledRackBaseWidth = 272f;
        public const float FilledRackWidthPerExtraCard = 20f;
        public const float FilledRackMaximumWidth = 370f;
        public const float RackCardOpacity = 0.95f;
        public const double EnterQueueSeconds = 0.20;
        public const float EntryAnimationAccelerationPerCard = 0.25f;
        public const float MinimumEntryAnimationDurationScale = 0.50f;
        public const float TriggerScale = 1.2f;
        public const double TriggerGrowSeconds = 0.14;
        public const double TriggerSettleSeconds = 0.18;
        public const float SequentialTriggerAccelerationPerCard = 0.10f;
        public const float MinimumSequentialTriggerDurationScale = 0.60f;
        public const double ExitSeconds = 0.38;
        public const double PreviewGrowSeconds = 0.12;
        public const float PreviewMouseXOffset = 34f;

        // Four stationary corner brackets use the same fixed accent color as
        // the remaining-turn counter; no particles orbit the card.
        public const float IdleEdgeMargin = 5f;
        public const float IdleEdgeBaseWidth = 1.65f;
        public const float IdleEdgeGlowWidth = 4.8f;
        public const float IdleEdgeBaseAlpha = 0.34f;
        public const float IdleEdgeGlowAlpha = 0.10f;

        // Code-drawn music staff behind the performance cards.
        public const int StaffLineCount = 5;
        public const float StaffWidth = 500f;
        public const float StaffLineSpacing = 22f;
        public const float StaffLineThickness = 2f;
        public const float StaffLineAlpha = 0.25f;
        public const int StaffInitialMarkerCount = 5;
        public const int StaffIdleMaximumMarkers = 7;
        public const int StaffPerformingMaximumMarkers = 15;
        // While the whole performance queue is resolving, the staff advances
        // through the same ambient simulation at this global fast-forward rate.
        // It accelerates marker travel, spawn timers and line-spacing cooldowns
        // together instead of injecting a fixed number of extra markers.
        public const float StaffPerformingFlowSpeedMultiplier = 1.75f;
        public const double StaffMarkerSpawnMinSeconds = 0.78;
        public const double StaffMarkerSpawnMaxSeconds = 1.22;
        public const double StaffMarkerSpawnRetrySeconds = 0.18;
        // Each staff line rolls its own cooldown after receiving a marker.
        // Cross-line glyphs briefly reserve their second line as well, while
        // neighboring lines only receive a tiny anti-overlap guard.
        public const float StaffSameLineSpawnCooldownMinSeconds = 0.34f;
        public const float StaffSameLineSpawnCooldownMaxSeconds = 0.72f;
        public const float StaffCrossLineSpawnCooldownMinSeconds = 0.10f;
        public const float StaffCrossLineSpawnCooldownMaxSeconds = 0.20f;
        public const float StaffAdjacentLineSpawnCooldownSeconds = 0.14f;
        public const float StaffMarkerSpeedMin = 25f;
        public const float StaffMarkerSpeedMax = 43f;
        public const float StaffMarkerRadiusMin = 4.5f;
        public const float StaffMarkerRadiusMax = 6f;
        public const float StaffMarkerGapPadding = 7f;
        public const float StaffMarkerBobAmplitude = 1.8f;
        public const float StaffMarkerBobSpeedMin = 1.1f;
        public const float StaffMarkerBobSpeedMax = 1.9f;
        public const float StaffMarkerAlpha = 0.62f;
        public const double StaffGlowFadeSeconds = 0.16;
        public const float StaffFlashGlowWidth = 7f;
        public const double StaffTriggerPulseSeconds = 0.34;
        public const float StaffSweepSpeed = 235f;
        public const float StaffSweepHalfWidth = 76f;
        public const int StaffSweepCount = 4;
        public const int StaffSparkleCount = 20;
        public const float StaffSparkleRadius = 2.6f;
        public const float StaffSparkleVerticalHalfExtent = 112f;
        public const double StaffPlayheadApproachSeconds = 0.20;
        public const double StaffPlayheadDepartureSeconds = 0.22;
        public const float StaffPlayheadEdgePadding = 38f;
        public const float StaffPlayheadHeight = 188f;
        public const float StaffPlayheadCoreWidth = 2.4f;
        public const float StaffPlayheadGlowWidth = 12f;
        public const double CardBurstSeconds = 0.46;
        public const int CardBurstParticleCount = 22;
        public const float CardBurstStartRadius = 32f;
        public const float CardBurstEndRadius = 104f;
    }

    public static class DiscardReveal
    {
        public const float RaiseDistance = 72f;
        public const float ScaleMultiplier = 1.08f;
        public const double RaiseSeconds = 0.14;
        public const double HoldSeconds = 0.22;
    }
}
