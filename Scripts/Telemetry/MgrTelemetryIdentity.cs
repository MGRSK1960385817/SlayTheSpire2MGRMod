using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Saves;

namespace MGRMod.Telemetry;

internal sealed record MgrTelemetryIdentityInfo(
    string InstallId,
    string SteamId);

/// <summary>
/// Owns MGR's explicit installation identifier and best-effort client-side
/// submission cooldown. This is deliberately not presented as authentication:
/// a modified client can replace both values and bypass the cooldown.
/// </summary>
internal static class MgrTelemetryIdentity
{
    private const long SubmissionCooldownSeconds = 60;
    private const string StateFileName = "mgr_telemetry_identity.json";

    private static readonly object Gate = new();
    private static IdentityState? _state;

    public static bool TryGet(
        SerializableRun run,
        out MgrTelemetryIdentityInfo identity,
        out string reason)
    {
        try
        {
            ulong steamId = PlatformUtil.GetLocalPlayerId(run.PlatformType);
            if (steamId == 0)
            {
                identity = null!;
                reason = "no valid local Steam ID is available";
                return false;
            }

            lock (Gate)
            {
                IdentityState state = GetOrCreateState();
                identity = new MgrTelemetryIdentityInfo(
                    state.InstallId,
                    steamId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            reason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            identity = null!;
            reason = $"identity initialization failed: {exception.GetType().Name}";
            return false;
        }
    }

    public static bool CanSubmit(
        MgrTelemetryIdentityInfo identity,
        DateTimeOffset now,
        out string reason)
    {
        lock (Gate)
        {
            IdentityState state = GetOrCreateState();
            if (!state.LastSubmissionUtcBySteamId.TryGetValue(
                    identity.SteamId,
                    out long lastSubmission))
            {
                reason = string.Empty;
                return true;
            }

            long elapsedSeconds = now.ToUnixTimeSeconds() - lastSubmission;
            if (elapsedSeconds >= SubmissionCooldownSeconds || elapsedSeconds < 0)
            {
                reason = string.Empty;
                return true;
            }

            reason = $"the same Steam ID submitted another run only {elapsedSeconds} seconds ago";
            return false;
        }
    }

    public static void MarkSubmitted(
        MgrTelemetryIdentityInfo identity,
        DateTimeOffset now)
    {
        lock (Gate)
        {
            IdentityState state = GetOrCreateState();
            state.LastSubmissionUtcBySteamId[identity.SteamId] = now.ToUnixTimeSeconds();
            SaveState(state);
        }
    }

    public static string BuildEventId(
        SerializableRun run,
        MgrTelemetryIdentityInfo identity)
    {
        string source = string.Join(
            '|',
            identity.InstallId,
            identity.SteamId,
            run.SerializableRng?.Seed ?? string.Empty,
            // StartTime is the run's actual persisted start timestamp. It keeps
            // deliberate restarts of the same seed distinct while remaining
            // stable when RitsuLib retries the same completed run.
            run.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"mgr_{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private static IdentityState GetOrCreateState()
    {
        if (_state is not null)
            return _state;

        string path = GetStatePath();
        try
        {
            if (File.Exists(path))
            {
                IdentityState? loaded = JsonSerializer.Deserialize<IdentityState>(
                    File.ReadAllText(path));
                if (loaded is not null
                    && Guid.TryParseExact(loaded.InstallId, "N", out _))
                {
                    loaded.LastSubmissionUtcBySteamId ??= [];
                    _state = loaded;
                    return loaded;
                }
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"Failed to read MGR telemetry identity; creating a new one: {exception.Message}");
        }

        _state = new IdentityState
        {
            InstallId = Guid.NewGuid().ToString("N")
        };
        SaveState(_state);
        return _state;
    }

    private static void SaveState(IdentityState state)
    {
        try
        {
            string path = GetStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    state,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception)
        {
            // Losing persistence weakens only best-effort deduplication. It must
            // never interfere with gameplay or run completion.
            Entry.Logger.Warn($"Failed to persist MGR telemetry identity: {exception.Message}");
        }
    }

    private static string GetStatePath() =>
        Path.Combine(Godot.OS.GetUserDataDir(), StateFileName);

    private sealed class IdentityState
    {
        [JsonPropertyName("install_id")]
        public string InstallId { get; set; } = string.Empty;

        [JsonPropertyName("last_submission_utc_by_steam_id")]
        public Dictionary<string, long> LastSubmissionUtcBySteamId { get; set; } = [];
    }
}
