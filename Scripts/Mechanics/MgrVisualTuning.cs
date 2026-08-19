using Godot;

namespace MGRMod.Mechanics;

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
        // The effect amount sits immediately outside the artwork's lower-right
        // corner instead of being centred beneath the whole slot.
        public static readonly Vector2 AmountLabelPosition = new(3f, 5f);
        public static readonly Vector2 AmountLabelSize = new(48f, 32f);
        public const int AmountLabelFontSize = 24;
        public const int AmountLabelOutlineSize = 8;
        public static readonly Color CurseAccentColor = new("78101c");
        // Filled Notes use a proportional, shader-drawn outer glow. A ratio is
        // used instead of source pixels so 64px and 384px artwork render alike.
        public const float ArtworkGlowRadiusRatio = 0.035f;
        public const float ArtworkGlowStrength = 0.38f;
        public const float ArtworkGlowCanvasMarginRatio = 0.06f;
        // Softens filled Notes without affecting empty-slot frames. Both the
        // artwork and its amount label inherit this presentation-only tint.
        public static readonly Color FilledNoteTint =
            new(0.95f, 0.95f, 0.95f, 0.95f);

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
        // Omnia holds its played card in the native centre presentation until
        // this awaited entrance beat completes. Keep it visible but brief so
        // the card can proceed into the Performance rack promptly.
        public const double OmniaNoteEntranceSeconds = 0.04;
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
        public const int RepeatedChordBurstParticleCount = 28;
        public const int SlotTransitionBurstParticleCount = 11;
        public const double EntranceBurstSeconds = 0.30;
        public const double ChordBurstSeconds = 0.46;
        public const double RepeatedChordBurstSeconds = 0.42;
        public const double SlotTransitionBurstSeconds = 0.36;
        public const float EntranceBurstEndRadius = 44f;
        public const float ChordBurstEndRadius = 82f;
        public const float RepeatedChordBurstEndRadius = 98f;
        public const float SlotTransitionBurstEndRadius = 58f;
        public const float NoteBurstStartRadius = 10f;
        public const float NoteBurstStarSize = 3.2f;
        public const float ChordTriggerScale = 1.16f;
        public const float RepeatedChordTriggerScale = 1.25f;
        public const double ChordTriggerGrowSeconds = 0.10;
        public const double ChordTriggerSettleSeconds = 0.16;
        // Extra chord passes get a distinct visual beat before their gameplay
        // effects resolve. Later triggers accelerate slightly without becoming
        // instantaneous, matching the rest of the note presentation cadence.
        public const double RepeatedChordBeatSeconds = 0.18;
        public const double MinimumRepeatedChordBeatSeconds = 0.10;
        public const double RepeatedChordBeatAccelerationPerTrigger = 0.012;

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

        // Ghost Note opacity drifts independently from its amount label. Each
        // Ghost samples a slightly different speed and phase so multiple Ghost
        // Notes never fade in lockstep.
        public const float GhostOpacityMinimum = 0.46f;
        public const float GhostOpacityMaximum = 1f;
        public const float GhostOpacityAngularSpeed = 1.65f;
        public const float GhostOpacitySpeedVariance = 0.28f;

        // Omnia Notes cycle through all five basic Note silhouettes and
        // the Starry silhouette while a rainbow flows across the current shape.
        public const double OmniaNoteShapeSeconds = 0.30;
        public const float OmniaNoteRainbowSpeed = 0.22f;
        public const float OmniaNoteRainbowFrequency = 1.35f;
    }

    public static class Performances
    {
        public static readonly Vector2 RackOffset = new(-3f, -432f); // 后面的数字绝对值越大 演奏堆越靠上
        // The staff is a presentation-only child of the rack. Its local offset
        // can be tuned without changing card positions, hover hitboxes, or the
        // coordinate chain used by performance animations.
        public static readonly Vector2 StaffOffset = new(0f, -16f);
        // CardOffsetY moves only cards, counters and their hitboxes relative to
        // the staff. Negative values move the whole interactive card upward.
        public const float CardOffsetY = -14f;
        public static readonly Vector2 MiniatureScale = new(0.345f, 0.345f);
        public static readonly Vector2 HoveredMiniatureScale = new(0.49f, 0.49f);
        public static readonly Vector2 PreviewScale = new(0.8f, 0.8f);
        public static readonly Color StaffLineColor = new("9b87c7");
        public static readonly Color StaffMarkerColor = new("d8c8ff");
        public static readonly Color StaffFlashColor = new("fff2b8");
        public static readonly Color PerformanceAccentColor = new("fff2b8");

        // Remaining Performance turns use a small floating beat marker above
        // the card rather than a purple badge on its lower-right corner.
        public static readonly Vector2 RemainingCounterSize = new(48f, 30f);
        public static readonly Color RemainingCounterColor =
            new(1f, 0.96f, 0.8f, 0.96f);
        public static readonly Color RemainingCounterOutlineColor =
            new(0.4f, 0.32f, 0.40f, 0.96f);
        public const int RemainingCounterFontSize = 23;
        public const int RemainingCounterOutlineSize = 4;
        public const float RemainingCounterTopGap = 4f;
        public const float RemainingCounterWingLength = 21f;
        public const float RemainingCounterSingleWingLengthScale = 0.76f;
        public const float RemainingCounterDoubleWingLengthScale = 0.88f;
        public const float RemainingCounterWingGap = 12f;
        public const float RemainingCounterWingSpacing = 4.5f;
        public const int RemainingCounterWingLineCount = 3;
        public const float RemainingCounterLineWidth = 1.4f;
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
        public const float RackCardBrightness = 0.95f;
        public const float RackCardOpacity = 0.95f;
        public const double EnterQueueSeconds = 0.20;
        public const float EntryAnimationAccelerationPerCard = 0.25f;
        public const float MinimumEntryAnimationDurationScale = 0.50f;
        public const float TriggerScale = 1.2f;
        // Keep the in-place pulse readable, but let the playhead hand off to the
        // next queued card sooner. These durations are presentation-only.
        public const double TriggerGrowSeconds = 0.12;
        public const double TriggerSettleSeconds = 0.15;
        public const float SequentialTriggerDurationMultiplierPerCard = 0.90f;
        public const float MinimumSequentialTriggerDurationScale = 0.60f;
        // Only explicit cinematic anticipation waits are shortened while a card
        // is being auto-played from the Performance rack. Gameplay commands are
        // still awaited in full and therefore remain strictly ordered.
        public const float PerformanceVfxWaitMultiplier = 0.70f;
        public const double MinimumPerformanceVfxWaitSeconds = 0.05;
        public const double ExitSeconds = 0.34;
        public const double PreviewGrowSeconds = 0.12;
        public const float PreviewMouseXOffset = 34f;

        // Maguro Dash uses a presentation-only card silhouette to cut through
        // the rack. It never enters the gameplay queue and therefore cannot
        // affect Performance counters or result-pile routing.
        public static readonly Vector2 FinisherCardSize = new(78f, 108f);
        public const float FinisherEntryDistance = 128f;
        public const double FinisherEntranceSeconds = 0.11;
        public const double FinisherFirstStepSeconds = 0.14;
        public const double FinisherStepAccelerationSeconds = 0.014;
        public const double FinisherMinimumStepSeconds = 0.075;
        public const float FinisherTrailLength = 112f;
        public const float FinisherExitDistance = 150f;
        public const double FinisherExitSeconds = 0.13;
        public const float FinisherEndedCardExitDurationScale = 0.58f;

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
        // A conventional thin/thick score barline remains at each edge. Stars
        // only spray outward during a Performance; the idle ends stay clean.
        public const float StaffEndBarThinWidth = 1.4f;
        public const float StaffEndBarThickWidth = 3.4f;
        public const float StaffEndBarSeparation = 5.5f;
        public const float StaffEndBarAlpha = 0.46f;
        public const int StaffEndSprayStarCount = 8;
        public const float StaffEndSprayStartDistance = 10f;
        public const float StaffEndSprayEndDistance = 72f;
        public const float StaffEndSprayVerticalSpread = 39f;
        public const float StaffEndSpraySpeed = 1.25f;
        public const float StaffEndSprayStarRadius = 3.4f;
        public const float StaffEndSprayStreakScale = 1f;
        // Markers are drawn below this vertical padding but are clipped exactly
        // at the staff's left and right edges.
        public const float StaffMarkerClipVerticalPadding = 54f;
        public const int StaffInitialMarkerCount = 5;
        public const int StaffIdleMaximumMarkers = 7;
        public const int StaffPerformingMaximumMarkers = 15;
        // While the whole performance queue is resolving, the staff advances
        // through the same ambient simulation at this global fast-forward rate.
        // It accelerates marker travel, spawn timers and line-spacing cooldowns
        // together instead of injecting a fixed number of extra markers.
        public const float StaffPerformingFlowSpeedMultiplier = 1.75f;
        // Ambient notes begin calmer than before. Every Performance card that is
        // actually played this combat raises spawn frequency by 8%, up to 1.8x.
        // Two played cards also unlock one extra simultaneous marker, capped at 5,
        // so the timer acceleration remains visible instead of hitting the old cap.
        public const double StaffMarkerSpawnMinSeconds = 1.02;
        public const double StaffMarkerSpawnMaxSeconds = 1.48;
        public const float StaffSpawnFrequencyIncreasePerPerformanceCard = 0.08f;
        public const float StaffMaximumCombatSpawnFrequencyMultiplier = 1.8f;
        public const int StaffPerformanceCardsPerAdditionalMarker = 2;
        public const int StaffMaximumAdditionalMarkersFromCombat = 5;
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
        public const double StaffPlayheadApproachSeconds = 0.17;
        public const double StaffPlayheadDepartureSeconds = 0.18;
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

    public static class FishVfx
    {
        // The main fish always begins at the requested 0.9 opacity. Two softer
        // copies trail it to turn the solid artwork into a moving afterimage.
        public const float MainOpacity = 0.90f;
        public const float DesiredWidth = 138f;
        public const float TargetOvershoot = 105f;
        public const float ArcHeight = 34f;
        public const float TravelSeconds = 0.30f;
        public const float TrailDelay = 0.035f;
        public const float FadeStartFraction = 0.58f;
        public const int ZIndex = 175;
    }

    public static class StarryNoteVfx
    {
        // Number of falling stars scattered across the upper combat field for
        // each actually generated Starry Note.
        public const int MinimumStarsPerNote = 5;
        public const int MaximumStarsPerNote = 8;
        public const int ZIndex = 190;
    }

    public static class MindMirageVfx
    {
        // The wave crosses the whole viewport, but only this short opening beat
        // is awaited. The remaining visual tail overlaps native Power apply UI.
        public const float LifetimeSeconds = 1.12f;
        public const float EntryBeatSeconds = 0.18f;
        public const float DistortionStrength = 1f;
    }

    public static class PrismaticVfx
    {
        public const float RingLifetimeSeconds = 0.34f;
        public const float DistortionStrength = 0.0055f;
        public const float MaximumRadius = 0.19f;
    }

    public static class GalaxyLampVfx
    {
        public const float ConvergenceSeconds = 0.24f;
        public const float LifetimeSeconds = 0.66f;
        public const int ZIndex = 38;
    }

    public static class MeteorAftermathVfx
    {
        public const float ConvergenceSeconds = 0.18f;
        public const float LifetimeSeconds = 0.68f;
        public const int ShardCount = 26;
        public const int ZIndex = 36;
    }

    public static class CubicPrismVfx
    {
        public const float RefractionLifetimeSeconds = 0.34f;
        public const float RefractionWidth = 0.026f;
        public const float RefractionStrength = 0.0035f;
    }

    public static class BirdVfx
    {
        // Fraction of the existing falling-bird lifetime used for its brief
        // high-altitude path cue. No additional gameplay wait is introduced.
        public const float PremonitionFraction = 0.34f;
    }

    public static class BlueCardVfx
    {
        // These cues are deliberately shorter and smaller than gold-card cast
        // bursts. They clarify one mechanical branch without becoming a new
        // blocking beat in Performance or Replay chains.
        public const float StandardLifetimeSeconds = 0.62f;
        public const float FinaleLifetimeSeconds = 0.78f;
        public const int ZIndex = 42;
    }

    public static class SpringStormVfx
    {
        public const float Opacity = 0.68f;
        public const float FlashHoldSeconds = 0.30f;
        public const float ShakeAmplitude = 10f;
        public const float ShakeTargetSeconds = 0.045f;
        public const float ShakeSmoothing = 24f;
        public const float DrawPadding = 16f;
        public const float FadeOutSeconds = 0.24f;
        public const int ZIndex = 20;
    }

    /// <summary>
    /// Full-screen, frame-by-frame presentation and target-local impact for
    /// Manimani (随之任之). The backdrop uses a dedicated CanvasLayer so the
    /// native played-card display cannot cover the centre of the artwork.
    /// </summary>
    public static class ManimaniVfx
    {
        // m1-m3 are quick animation beats; m4 is the deliberate anticipation
        // hold. Damage begins on the same beat that m6 appears.
        public const float Frame1Seconds = 0.2f;
        public const float Frame2Seconds = 0.2f;
        public const float Frame3Seconds = 0.2f;
        public const float Frame4Seconds = 0.5f;
        // m6 starts fading immediately upon appearing.
        public const float OutcomeFadeSeconds = 0.2f;

        // Source art is 3000x1500. Cover scaling fills the combat viewport and
        // crops only the excess edge caused by a different aspect ratio.
        public const float BackdropScale = 1f;
        public static readonly Vector2 BackdropOffset = new(-100f, 0f);
        public const float BackdropBrightness = 0.98f;
        // Every frame in the conditional full-screen sequence uses the same
        // opacity so texture changes do not introduce unintended brightness
        // steps. The outcome frame is m6; the unused m5 asset was removed.
        public const float Frame1Opacity = 0.9f;
        public const float Frame2Opacity = 0.9f;
        public const float Frame3Opacity = 0.9f;
        public const float Frame4Opacity = 0.9f;
        public const float OutcomeOpacity = 0.9f;
        // Higher than the native combat/played-card canvases, while remaining
        // below MGR's hover preview (90) and full-screen post-process (96).
        public const int BackdropCanvasLayer = 85;

        public const float ImpactLifetimeSeconds = 0.30f;
        public const float ImpactScale = 1f;
        public const float FatalImpactScale = 1.5f;
        public const int ImpactShardCount = 42;
        public const int ImpactZIndex = 220;
        public static readonly Color ImpactFireColor = new("ff542e");
        public const float ImpactFireScale = 1.35f;
        public const float FatalImpactFireScale = 1.65f;
        // Linear playback ratio for NoteChannel.ogg at the final impact. The
        // m1-m4 image transitions are silent. This replaces the old
        // heavy_attack.mp3 hit and stays louder than ordinary note generation.
        public const float ImpactNoteSoundVolume = 0.42f;
        // The satisfied Fatal branch replaces the note cue with these two
        // impact layers. The gunshot layer matches MGR's gun-themed cards.
        public const float FatalGaseousImpactSoundVolume = 0.75f;
        public const float FatalGunshotImpactSoundVolume = 0.90f;
        // The burn layer is played only for the satisfied Fatal branch.
        public const float FatalFireSoundVolume = 0.86f;
    }

    public static class MeteorShowerVfx
    {
        // Time from one meteor beginning its fall to the next meteor beginning.
        // This controls only the sky visual and does not change damage timing.
        // It is intentionally shorter than one meteor's 0.30-0.38 second
        // flight, so consecutive meteors overlap like an actual shower.
        public const float SpawnIntervalMinSeconds = 0.07f;
        public const float SpawnIntervalMaxSeconds = 0.14f;
    }
}
