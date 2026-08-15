using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SlayTheSpire2MGRMod.Telemetry;

/// <summary>
/// Detects an actual Loadout2 mutation instead of treating installation alone
/// as evidence that a run was modified. The marker is persisted by run identity
/// so save-and-quit cannot accidentally turn a modified run back into a clean one.
/// This remains best-effort compatibility with an optional third-party mod.
/// </summary>
internal static class MgrLoadoutUsageTracker
{
    private const string LoadoutManifestId = "Loadout";
    private const string LoadoutMutationServiceType =
        "Loadout.Services.Actions.LoadoutImmediateMutationService";
    private const string StateFileName = "mgr_telemetry_loadout_runs.json";
    private const int MaximumRememberedRuns = 256;

    private static readonly object Gate = new();
    private static Harmony? _harmony;
    private static TrackerState? _state;
    private static bool _unkeyedCurrentRunUsed;
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        bool loadoutLoaded = ModManager.GetLoadedMods().Any(mod =>
            string.Equals(
                mod.manifest?.id,
                LoadoutManifestId,
                StringComparison.OrdinalIgnoreCase));
        if (!loadoutLoaded)
            return;

        try
        {
            Type? serviceType = AccessTools.TypeByName(LoadoutMutationServiceType);
            System.Reflection.MethodInfo? applyMethod =
                AccessTools.DeclaredMethod(serviceType, "ApplyAsync");
            System.Reflection.MethodInfo? prefixMethod = AccessTools.DeclaredMethod(
                typeof(MgrLoadoutUsageTracker),
                nameof(OnLoadoutMutation));
            if (applyMethod is null || prefixMethod is null)
            {
                Entry.Logger.Warn(
                    "Loadout is installed, but its mutation entry point was not found. " +
                    "MGR will not reject runs merely because Loadout is installed.");
                return;
            }

            _harmony = new Harmony($"{Entry.ModId}.telemetry.loadout-usage");
            _harmony.Patch(applyMethod, prefix: new HarmonyMethod(prefixMethod));
            Entry.Logger.Info(
                "MGR telemetry will exclude only runs that actually invoke a Loadout mutation.");
        }
        catch (Exception exception)
        {
            // An optional mod update must not disable MGR or fall back to the
            // overly broad "installed means dirty" rule.
            Entry.Logger.Warn(
                $"Could not enable precise Loadout usage detection; installation alone " +
                $"will not reject telemetry: {exception.Message}");
        }
    }

    public static void BeginRun()
    {
        lock (Gate)
            _unkeyedCurrentRunUsed = false;
    }

    public static bool WasUsedInRun(SerializableRun run)
    {
        lock (Gate)
            return _unkeyedCurrentRunUsed || GetOrCreateState().RunKeys.Contains(BuildRunKey(run));
    }

    public static void FinishRun(SerializableRun run)
    {
        lock (Gate)
        {
            TrackerState state = GetOrCreateState();
            bool changed = state.RunKeys.Remove(BuildRunKey(run));
            _unkeyedCurrentRunUsed = false;
            if (changed)
                SaveState(state);
        }
    }

    private static void OnLoadoutMutation()
    {
        lock (Gate)
        {
            try
            {
                RunManager manager = RunManager.Instance;
                if (!manager.IsInProgress || manager.IsCleaningUp)
                    return;

                SerializableRun run = manager.ToSave(null);
                TrackerState state = GetOrCreateState();
                string runKey = BuildRunKey(run);
                if (state.RunKeys.Add(runKey))
                {
                    TrimOldKeys(state);
                    SaveState(state);
                }
            }
            catch (Exception exception)
            {
                // If the live run cannot be serialized at this exact moment,
                // keep the current in-memory run conservative without breaking
                // the Loadout action itself.
                _unkeyedCurrentRunUsed = true;
                Entry.Logger.Warn(
                    $"A Loadout mutation was detected but its run key could not be persisted: " +
                    $"{exception.Message}");
            }
        }
    }

    private static string BuildRunKey(SerializableRun run)
    {
        string source = string.Join(
            '|',
            run.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture),
            run.SerializableRng?.Seed ?? string.Empty,
            run.GameMode.ToString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
    }

    private static TrackerState GetOrCreateState()
    {
        if (_state is not null)
            return _state;

        try
        {
            string path = GetStatePath();
            if (File.Exists(path))
            {
                TrackerState? loaded = JsonSerializer.Deserialize<TrackerState>(
                    File.ReadAllText(path));
                if (loaded is not null)
                {
                    loaded.RunKeys ??= [];
                    _state = loaded;
                    return loaded;
                }
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn($"Failed to read MGR Loadout run markers: {exception.Message}");
        }

        _state = new TrackerState();
        return _state;
    }

    private static void TrimOldKeys(TrackerState state)
    {
        while (state.RunKeys.Count > MaximumRememberedRuns)
            state.RunKeys.Remove(state.RunKeys.First());
    }

    private static void SaveState(TrackerState state)
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
            Entry.Logger.Warn($"Failed to persist MGR Loadout run marker: {exception.Message}");
        }
    }

    private static string GetStatePath() =>
        Path.Combine(Godot.OS.GetUserDataDir(), StateFileName);

    private sealed class TrackerState
    {
        [JsonPropertyName("run_keys")]
        public HashSet<string> RunKeys { get; set; } = [];
    }
}
