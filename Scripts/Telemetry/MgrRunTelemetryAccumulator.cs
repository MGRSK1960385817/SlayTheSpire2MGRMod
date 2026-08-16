using System.Text.Json.Nodes;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace MGRMod.Telemetry;

/// <summary>
/// Keeps only compact, run-wide MGR mechanic totals. The totals live in a
/// RitsuLib run-saved-data slot, so saving and loading a combat rewinds them to
/// the same checkpoint as the run instead of losing or double-counting data.
/// </summary>
internal static class MgrRunTelemetryAccumulator
{
    private const string SavedDataKey = "telemetry_aggregate";
    private static readonly AsyncLocal<int> NoteDamageDepth = new();

    private static RunSavedData<MgrTelemetryRunState>? _savedState;
    private static RunState? _run;

    public static void RegisterSavedData()
    {
        if (_savedState is not null)
            return;

        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            _savedState = RitsuLibFramework
                .GetRunSavedDataStore(Entry.ModId)
                .Register(
                    SavedDataKey,
                    () => new MgrTelemetryRunState(),
                    new RunSavedDataOptions
                    {
                        SchemaVersion = 1,
                        WritePolicy = RunSavedDataWritePolicy.WhenNonDefault
                    });
        }
    }

    public static void BeginRun(RunState run, bool isNewRun)
    {
        _run = run;
        NoteDamageDepth.Value = 0;

        RunSavedData<MgrTelemetryRunState>? savedState = _savedState;
        if (savedState is null)
            return;

        if (isNewRun)
            savedState.Set(run, new MgrTelemetryRunState());
        else
            _ = savedState.Get(run);
    }

    public static void RecordNoteGenerated(Player player, NoteKind kind)
    {
        Modify(player, state =>
        {
            state.NotesGenerated = SaturatingAdd(state.NotesGenerated, 1);
            string key = GetNoteKindKey(kind);
            state.NotesByKind[key] = SaturatingAdd(
                state.NotesByKind.GetValueOrDefault(key),
                1);
        });
    }

    public static void RecordChordCompleted(Player player) =>
        Modify(player, state =>
            state.ChordsCompleted = SaturatingAdd(state.ChordsCompleted, 1));

    public static void RecordChordEffectTrigger(Player player) =>
        Modify(player, state =>
            state.ChordEffectTriggers = SaturatingAdd(state.ChordEffectTriggers, 1));

    public static void RecordPerformanceTrigger(Player player) =>
        Modify(player, state =>
            state.PerformanceTriggers = SaturatingAdd(state.PerformanceTriggers, 1));

    public static IDisposable BeginNoteDamage() => new NoteDamageScope();

    public static void RecordOutgoingDamage(
        Creature target,
        DamageResult result,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!target.IsEnemy || result.UnblockedDamage <= 0)
            return;

        // Match the base game's ExtraFields.DamageDealt definition exactly:
        // only damage whose dealer belongs to the MGR player is classified.
        Player? sourcePlayer = dealer?.Player;
        if (sourcePlayer?.Character is not MgrCharacter)
            return;

        Modify(sourcePlayer, state =>
        {
            if (NoteDamageDepth.Value > 0)
            {
                state.NoteDamage = SaturatingAdd(
                    state.NoteDamage,
                    result.UnblockedDamage);
            }
            else if (cardSource is not null)
            {
                state.CardDamage = SaturatingAdd(
                    state.CardDamage,
                    result.UnblockedDamage);
            }
            else
            {
                state.OtherPlayerDamage = SaturatingAdd(
                    state.OtherPlayerDamage,
                    result.UnblockedDamage);
            }
        });
    }

    public static JsonObject BuildSnapshot(int totalDamageDealt)
    {
        MgrTelemetryRunState state = GetCurrentState(out bool trackingAvailable);
        long classifiedDamage = (long)state.CardDamage
                                + state.NoteDamage
                                + state.OtherPlayerDamage;
        int unclassifiedDamage = classifiedDamage >= totalDamageDealt
            ? 0
            : (int)Math.Min(int.MaxValue, totalDamageDealt - classifiedDamage);
        JsonObject noteKinds = new();
        foreach (NoteKind kind in Enum.GetValues<NoteKind>())
        {
            string key = GetNoteKindKey(kind);
            noteKinds[key] = state.NotesByKind.GetValueOrDefault(key);
        }

        return new JsonObject
        {
            // These totals now share the run's save/checkpoint lifecycle. An SL
            // restores the saved aggregate before replaying the combat.
            ["tracking_complete"] = trackingAvailable,
            ["reload_safe"] = trackingAvailable,
            ["notes_generated"] = state.NotesGenerated,
            ["notes_by_kind"] = noteKinds,
            ["chords_completed"] = state.ChordsCompleted,
            ["chord_effect_triggers"] = state.ChordEffectTriggers,
            ["performance_triggers"] = state.PerformanceTriggers,
            ["damage_by_source"] = new JsonObject
            {
                ["card"] = state.CardDamage,
                ["note"] = state.NoteDamage,
                ["other"] = state.OtherPlayerDamage,
                // This keeps the categories reconcilable with the base game's
                // authoritative total if a new damage path bypasses our hook.
                ["unclassified"] = unclassifiedDamage
            }
        };
    }

    public static bool IsSane(int totalDamageDealt, out string reason)
    {
        MgrTelemetryRunState state = GetCurrentState(out bool trackingAvailable);
        if (!trackingAvailable)
        {
            reason = "the MGR run-saved telemetry state is unavailable";
            return false;
        }
        if (state.NotesGenerated is < 0 or > MgrRunSanityValidator.MaximumMechanicCount
            || state.ChordsCompleted is < 0 or > MgrRunSanityValidator.MaximumMechanicCount
            || state.ChordEffectTriggers is < 0 or > MgrRunSanityValidator.MaximumMechanicCount
            || state.PerformanceTriggers is < 0 or > MgrRunSanityValidator.MaximumMechanicCount)
        {
            reason = "an MGR mechanic counter is out of range";
            return false;
        }

        if (state.NotesByKind.Values.Any(
                value => value is < 0 or > MgrRunSanityValidator.MaximumMechanicCount)
            || state.NotesByKind.Values.Sum(value => (long)value) != state.NotesGenerated)
        {
            reason = "the per-kind note counts do not match the total note count";
            return false;
        }

        if (state.CardDamage is < 0 or > MgrRunSanityValidator.MaximumDamagePerSource
            || state.NoteDamage is < 0 or > MgrRunSanityValidator.MaximumDamagePerSource
            || state.OtherPlayerDamage is < 0 or > MgrRunSanityValidator.MaximumDamagePerSource)
        {
            reason = "an MGR damage counter is out of range";
            return false;
        }

        long classifiedDamage = (long)state.CardDamage
                                + state.NoteDamage
                                + state.OtherPlayerDamage;
        if (classifiedDamage > totalDamageDealt)
        {
            reason =
                $"classified MGR damage {classifiedDamage} exceeds the base-game total {totalDamageDealt}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static void Reset()
    {
        // Do not clear the saved slot here. RunEnded can serialize after this
        // callback, and a newly started run gets an explicit fresh state.
        _run = null;
        NoteDamageDepth.Value = 0;
    }

    private static void Modify(Player player, Action<MgrTelemetryRunState> mutation)
    {
        if (player.RunState is not RunState run)
            return;

        _run = run;
        _savedState?.Modify(run, state =>
        {
            Normalize(state);
            mutation(state);
        });
    }

    private static MgrTelemetryRunState GetCurrentState(out bool trackingAvailable)
    {
        if (_run is not null && _savedState is not null)
        {
            MgrTelemetryRunState state = _savedState.Get(_run);
            Normalize(state);
            trackingAvailable = true;
            return state;
        }

        trackingAvailable = false;
        return new MgrTelemetryRunState();
    }

    private static void Normalize(MgrTelemetryRunState state) =>
        state.NotesByKind ??= new Dictionary<string, int>(StringComparer.Ordinal);

    private static int SaturatingAdd(int current, int amount) =>
        amount <= 0 || current >= int.MaxValue - amount
            ? amount <= 0 ? current : int.MaxValue
            : current + amount;

    private static string GetNoteKindKey(NoteKind kind) => kind switch
    {
        NoteKind.Attack => "attack",
        NoteKind.Skill => "skill",
        NoteKind.Power => "ability",
        NoteKind.Status => "status",
        NoteKind.Curse => "curse",
        NoteKind.Starry => "starry",
        NoteKind.Ghost => "ghost",
        NoteKind.OmniaNote => "omnia",
        _ => kind.ToString().ToLowerInvariant()
    };

    private sealed class NoteDamageScope : IDisposable
    {
        private bool _disposed;

        public NoteDamageScope() => NoteDamageDepth.Value++;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NoteDamageDepth.Value = Math.Max(0, NoteDamageDepth.Value - 1);
        }
    }
}

/// <summary>
/// Serializable, aggregate-only state embedded in the current run save.
/// Public setters are required by the run-saved-data serializer.
/// </summary>
public sealed class MgrTelemetryRunState
{
    public int NotesGenerated { get; set; }
    public Dictionary<string, int> NotesByKind { get; set; } =
        new(StringComparer.Ordinal);
    public int ChordsCompleted { get; set; }
    public int ChordEffectTriggers { get; set; }
    public int PerformanceTriggers { get; set; }
    public int CardDamage { get; set; }
    public int NoteDamage { get; set; }
    public int OtherPlayerDamage { get; set; }
}
