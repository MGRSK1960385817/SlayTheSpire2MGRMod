using System.Text.Json.Nodes;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MGRMod.Characters;
using MGRMod.Mechanics;

namespace MGRMod.Telemetry;

/// <summary>
/// Keeps only compact, run-wide MGR mechanic totals. It deliberately does not
/// retain individual card plays, damage events, note order or combat snapshots.
/// </summary>
internal static class MgrRunTelemetryAccumulator
{
    private static readonly AsyncLocal<int> NoteDamageDepth = new();
    private static readonly Dictionary<NoteKind, int> NotesByKind = [];

    private static IRunState? _run;
    private static int _notesGenerated;
    private static int _chordsCompleted;
    private static int _chordEffectTriggers;
    private static int _performanceTriggers;
    private static int _cardDamage;
    private static int _noteDamage;
    private static int _otherPlayerDamage;

    public static void RecordNoteGenerated(Player player, NoteKind kind)
    {
        EnsureRun(player);
        _notesGenerated = SaturatingAdd(_notesGenerated, 1);
        NotesByKind[kind] = SaturatingAdd(NotesByKind.GetValueOrDefault(kind), 1);
    }

    public static void RecordChordCompleted(Player player)
    {
        EnsureRun(player);
        _chordsCompleted = SaturatingAdd(_chordsCompleted, 1);
    }

    public static void RecordChordEffectTrigger(Player player)
    {
        EnsureRun(player);
        _chordEffectTriggers = SaturatingAdd(_chordEffectTriggers, 1);
    }

    public static void RecordPerformanceTrigger(Player player)
    {
        EnsureRun(player);
        _performanceTriggers = SaturatingAdd(_performanceTriggers, 1);
    }

    public static IDisposable BeginNoteDamage() => new NoteDamageScope();

    public static void RecordOutgoingDamage(
        Creature target,
        DamageResult result,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!target.IsEnemy || result.UnblockedDamage <= 0)
            return;

        Player? sourcePlayer = dealer?.Player ?? cardSource?.Owner;
        if (sourcePlayer?.Character is not MgrCharacter)
            return;

        EnsureRun(sourcePlayer);
        if (NoteDamageDepth.Value > 0)
        {
            _noteDamage = SaturatingAdd(_noteDamage, result.UnblockedDamage);
        }
        else if (cardSource is not null)
        {
            _cardDamage = SaturatingAdd(_cardDamage, result.UnblockedDamage);
        }
        else
        {
            _otherPlayerDamage = SaturatingAdd(
                _otherPlayerDamage,
                result.UnblockedDamage);
        }
    }

    public static JsonObject BuildSnapshot(int reloadCount)
    {
        JsonObject noteKinds = new();
        foreach (NoteKind kind in Enum.GetValues<NoteKind>())
            noteKinds[GetNoteKindKey(kind)] = NotesByKind.GetValueOrDefault(kind);

        return new JsonObject
        {
            // Reloading can rewind combat state while this in-memory aggregate
            // cannot. Keep the totals useful, but make partial/duplicated samples
            // explicitly filterable in PostHog.
            ["tracking_complete"] = reloadCount == 0,
            ["notes_generated"] = _notesGenerated,
            ["notes_by_kind"] = noteKinds,
            ["chords_completed"] = _chordsCompleted,
            ["chord_effect_triggers"] = _chordEffectTriggers,
            ["performance_triggers"] = _performanceTriggers,
            ["damage_by_source"] = new JsonObject
            {
                ["card"] = _cardDamage,
                ["note"] = _noteDamage,
                ["other"] = _otherPlayerDamage
            }
        };
    }

    public static bool IsSane(out string reason)
    {
        if (_notesGenerated is < 0 or > MgrRunSanityValidator.MaximumMechanicCount
            || _chordsCompleted is < 0 or > MgrRunSanityValidator.MaximumMechanicCount
            || _chordEffectTriggers is < 0 or > MgrRunSanityValidator.MaximumMechanicCount
            || _performanceTriggers is < 0 or > MgrRunSanityValidator.MaximumMechanicCount)
        {
            reason = "an MGR mechanic counter is out of range";
            return false;
        }

        if (NotesByKind.Values.Any(value => value is < 0 or > MgrRunSanityValidator.MaximumMechanicCount)
            || NotesByKind.Values.Sum(value => (long)value) != _notesGenerated)
        {
            reason = "the per-kind note counts do not match the total note count";
            return false;
        }

        if (_cardDamage is < 0 or > MgrRunSanityValidator.MaximumDamagePerSource
            || _noteDamage is < 0 or > MgrRunSanityValidator.MaximumDamagePerSource
            || _otherPlayerDamage is < 0 or > MgrRunSanityValidator.MaximumDamagePerSource)
        {
            reason = "an MGR damage counter is out of range";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static void Reset()
    {
        _run = null;
        _notesGenerated = 0;
        _chordsCompleted = 0;
        _chordEffectTriggers = 0;
        _performanceTriggers = 0;
        _cardDamage = 0;
        _noteDamage = 0;
        _otherPlayerDamage = 0;
        NotesByKind.Clear();
        NoteDamageDepth.Value = 0;
    }

    private static void EnsureRun(Player player)
    {
        if (ReferenceEquals(_run, player.RunState))
            return;

        Reset();
        _run = player.RunState;
    }

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
