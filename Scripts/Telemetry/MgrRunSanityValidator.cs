using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;

namespace MGRMod.Telemetry;

/// <summary>
/// Rejects structurally impossible or wildly out-of-range run records before
/// serialization. Limits are intentionally generous so unusual legitimate MGR
/// builds survive while corrupted or trivially fabricated payloads do not.
/// </summary>
internal static class MgrRunSanityValidator
{
    // Single source of truth for every hard numeric ceiling used when deciding
    // whether a telemetry record is accepted. Keep MGR_TELEMETRY.md in sync.
    private const int MaximumActs = 6;
    private const int MaximumMapPoints = 100;
    private const int MaximumRoomsPerMapPoint = 16;
    private const int MaximumChoicesPerMapPoint = 128;
    private const int MaximumDeckSize = 1000;
    private const int MaximumRelics = 100;
    private const int MaximumPotions = 11;
    private const int MaximumAscension = 20;
    private const int MaximumHitPoints = 1_000;
    private const int MaximumEnergy = 10;
    private const int MaximumGold = 10000;
    private const int MaximumReloads = 1000;
    private const long MaximumDurationSeconds = 24L * 60L * 60L;
    private const int MaximumSerializedCharacters = 2_000_000;

    // The accumulator performs these checks because it owns the counters, but
    // their limits live here so all telemetry acceptance limits are searchable
    // and maintained in one place.
    internal const int MaximumMechanicCount = 1_000_000;
    internal const int MaximumDamagePerSource = 100_000_000;

    public static bool IsValidSource(RunEndedEvent evt, out string reason)
    {
        SerializableRun run = evt.Run;
        int floorReached = run.MapPointHistory.Sum(act => act.Count);
        long durationSeconds = MgrTelemetryEligibility.GetDurationSeconds(evt);

        if (run.Acts.Count is < 1 or > MaximumActs)
            return Reject($"act count {run.Acts.Count} is out of range", out reason);

        if (run.MapPointHistory.Count > MaximumActs)
            return Reject($"map act count {run.MapPointHistory.Count} is out of range", out reason);

        if (floorReached is < 0 or > MaximumMapPoints)
            return Reject($"map point count {floorReached} is out of range", out reason);

        if (durationSeconds is <= 0 or > MaximumDurationSeconds)
            return Reject($"duration {durationSeconds} seconds is out of range", out reason);

        if (run.Ascension is < 0 or > MaximumAscension)
            return Reject($"ascension {run.Ascension} is out of range", out reason);

        if (run.NumReloads is < 0 or > MaximumReloads)
            return Reject($"reload count {run.NumReloads} is out of range", out reason);

        SerializablePlayer player = run.Players.Single();
        if (player.Deck.Count is < 1 or > MaximumDeckSize)
            return Reject($"deck size {player.Deck.Count} is out of range", out reason);

        if (player.Relics.Count > MaximumRelics)
            return Reject($"relic count {player.Relics.Count} is out of range", out reason);

        if (player.Potions.Count > MaximumPotions)
            return Reject($"potion count {player.Potions.Count} is out of range", out reason);

        if (player.MaxHp is < 1 or > MaximumHitPoints
            || player.CurrentHp < 0
            || player.CurrentHp > player.MaxHp)
        {
            return Reject(
                $"final HP {player.CurrentHp}/{player.MaxHp} is out of range",
                out reason);
        }

        if (player.MaxEnergy is < 0 or > MaximumEnergy)
            return Reject($"maximum energy {player.MaxEnergy} is out of range", out reason);

        if (player.Gold is < 0 or > MaximumGold)
            return Reject($"gold {player.Gold} is out of range", out reason);

        foreach (List<MapPointHistoryEntry> act in run.MapPointHistory)
        {
            foreach (MapPointHistoryEntry mapPoint in act)
            {
                if (mapPoint.Rooms.Count > MaximumRoomsPerMapPoint)
                {
                    return Reject(
                        $"a map point contains {mapPoint.Rooms.Count} rooms",
                        out reason);
                }

                PlayerMapPointHistoryEntry playerEntry;
                try
                {
                    playerEntry = mapPoint.GetEntry(player.NetId);
                }
                catch
                {
                    return Reject("a map point has no entry for the local player", out reason);
                }

                if (playerEntry.MaxHp is < 1 or > MaximumHitPoints
                    || playerEntry.CurrentHp < 0
                    || playerEntry.CurrentHp > playerEntry.MaxHp
                    || playerEntry.CurrentGold is < 0 or > MaximumGold)
                {
                    return Reject("a map-point player snapshot is out of range", out reason);
                }

                int choiceCount =
                    playerEntry.CardChoices.Count
                    + playerEntry.RelicChoices.Count
                    + playerEntry.PotionChoices.Count
                    + playerEntry.AncientChoices.Count
                    + playerEntry.EventChoices.Count;
                if (choiceCount > MaximumChoicesPerMapPoint)
                {
                    return Reject(
                        $"a map point contains {choiceCount} recorded choices",
                        out reason);
                }
            }
        }

        if (!MgrRunTelemetryAccumulator.IsSane(
                player.ExtraFields?.DamageDealt ?? 0,
                out reason))
            return false;

        reason = string.Empty;
        return true;
    }

    public static bool IsValidPayload(MgrRunMetrics metrics, out string reason)
    {
        int serializedCharacters = metrics.Payload.ToJsonString().Length;
        if (serializedCharacters > MaximumSerializedCharacters)
        {
            return Reject(
                $"serialized payload contains {serializedCharacters} characters",
                out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool Reject(string rejectionReason, out string reason)
    {
        reason = rejectionReason;
        return false;
    }
}
