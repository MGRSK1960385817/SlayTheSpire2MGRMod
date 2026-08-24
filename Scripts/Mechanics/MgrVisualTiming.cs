using MegaCrit.Sts2.Core.Entities.Players;

namespace MGRMod.Mechanics;

/// <summary>
/// Applies local presentation pacing without entering gameplay state or the
/// multiplayer protocol. Tower 2 uses a player count above one as its standard
/// multiplayer check, so host and clients derive the same animation duration.
/// </summary>
internal static class MgrVisualTiming
{
    public static float GetAnimationDurationScale(Player player) =>
        1f / Math.Max(1, player.RunState.Players.Count);

    public static float ScaleBlockingVisualWait(
        Player player,
        float normalSeconds)
    {
        if (normalSeconds <= 0f)
            return normalSeconds;

        float durationScale = GetAnimationDurationScale(player);
        return MathF.Max(
            (float)MgrVisualTuning.Multiplayer.MinimumBlockingVisualWaitSeconds *
                durationScale,
            normalSeconds * durationScale);
    }

    public static double ScaleVisualDuration(
        Player player,
        double normalSeconds)
    {
        if (normalSeconds <= 0.0)
            return normalSeconds;

        float durationScale = GetAnimationDurationScale(player);
        return Math.Max(
            MgrVisualTuning.Multiplayer.MinimumBlockingVisualWaitSeconds *
                durationScale,
            normalSeconds * durationScale);
    }
}
