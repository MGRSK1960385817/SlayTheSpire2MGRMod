using MegaCrit.Sts2.Core.Entities.Players;

namespace MGRMod.Mechanics;

/// <summary>
/// Immutable information exposed to a card when its final performance ends.
/// </summary>
public sealed record PerformanceCompletionContext(
    Player Player,
    int InitialPerformanceTurns,
    bool WillExhaust);
