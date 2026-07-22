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
        public static readonly Vector2 RackOffset = new(0f, -430f);
        public static readonly Vector2 ArtworkScale = new(0.68f, 0.68f);

        public const int RackZIndex = 50;
        public const float DesiredSlotSpacing = 96f;
        public const float MaximumRackWidth = 480f;
        public const float SlotRadius = 42f;
        public const int EmptySlotDashCount = 12;
        public const float EmptySlotDashFill = 0.48f;
        public const float EmptySlotDashWidth = 3f;

        // Repeated note/chord activity in one turn accelerates presentation, but
        // both paths retain a visible minimum duration.
        public const double FirstNoteEntranceSeconds = 0.28;
        public const double MinimumNoteEntranceSeconds = 0.10;
        public const double NoteEntranceAccelerationPerNote = 0.018;
        public const double FirstChordHoldSeconds = 0.42;
        public const double MinimumChordHoldSeconds = 0.12;
        public const double ChordHoldAccelerationPerChord = 0.05;
        public const int FastChordCommandThreshold = 2;

        // Newly generated notes pop in one after another. ChannelSingleNote awaits
        // this animation, so cards that create several notes naturally serialize.
        public const float EntranceStartScale = 0.28f;
        public const float EntranceOvershootScale = 1.18f;
        public const float EntranceGrowFraction = 0.62f;
        public const float EntranceStartYOffset = 18f;

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
    }

    public static class Performances
    {
        public static readonly Vector2 RackOffset = new(0f, -650f);
        public static readonly Vector2 MiniatureScale = new(0.25f, 0.25f);
        public static readonly Vector2 HoveredMiniatureScale = new(0.29f, 0.29f);
        public static readonly Vector2 PreviewScale = new(0.68f, 0.68f);

        public const int RackZIndex = 55;
        public const float DesiredSpacing = 52f;
        public const float MaximumWidth = 520f;
        public const double EnterQueueSeconds = 0.28;
        public const float TriggerScale = 1.2f;
        public const double TriggerGrowSeconds = 0.14;
        public const double TriggerSettleSeconds = 0.18;
        public const double ExitSeconds = 0.38;
        public const double PreviewGrowSeconds = 0.12;
        public const float PreviewMouseXOffset = 34f;
    }

    public static class DiscardReveal
    {
        public const float RaiseDistance = 72f;
        public const float ScaleMultiplier = 1.08f;
        public const double RaiseSeconds = 0.14;
        public const double HoldSeconds = 0.22;
    }
}
