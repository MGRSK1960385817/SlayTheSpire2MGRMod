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
        public const int CircleSegments = 48;

        // A completed chord remains visible for this long before the rack clears.
        public const double ChordHoldSeconds = 0.45;

        // Newly generated notes pop in one after another. ChannelSingleNote awaits
        // this animation, so cards that create several notes naturally serialize.
        public const float EntranceStartScale = 0.28f;
        public const float EntranceOvershootScale = 1.18f;
        public const double EntranceGrowSeconds = 0.13;
        public const double EntranceSettleSeconds = 0.09;
        public const float EntranceStartYOffset = 18f;
        public const float EntranceFlashScale = 1.38f;
        public const float EntranceFlashAlpha = 0.52f;

        // Idle movement. Phase staggering keeps a row from moving as one rigid bar.
        public const float BobAmplitude = 5f;
        public const float BobAngularSpeed = 1.75f;
        public const float BreathAmplitude = 0.055f;
        public const float BreathAngularSpeed = 2.05f;
        public const float PhaseStep = 0.72f;
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
