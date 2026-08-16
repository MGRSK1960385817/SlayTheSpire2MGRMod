using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Telemetry;

namespace MGRMod.Telemetry;

/// <summary>
/// Sends an explicitly allow-listed, single-player MGR balance payload.
/// RitsuLib still owns consent, persistence, retries and network-failure isolation,
/// but MGR deliberately does not request RitsuLib's full RunHistory payload.
/// </summary>
public static class MgrTelemetry
{
    // MGR uses its own consent item and never requests RitsuLib's full
    // RunHistory payload. Development builds do not migrate the old request ID.
    private const string CleanRunRequestId = "mgr_clean_run_metrics";
    private const string CleanRunEventName = "mgr_run_completed";

    // A PostHog project ingestion token is intentionally client-readable. Never
    // replace it with a personal/admin key capable of reading or managing data.
    private const string PostHogHost = "https://us.i.posthog.com";
    private const string PostHogProjectApiKey = "phc_ABLRS6Ap6jd2w4JbaJG9Y7tFhAJ4uQbgTefbSKR97aXq";

    private static ITelemetryClient? _client;
    private static IDisposable? _runStartedSubscription;
    private static IDisposable? _runLoadedSubscription;
    private static IDisposable? _runEndedSubscription;
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        try
        {
            MgrRunTelemetryAccumulator.RegisterSavedData();
        }
        catch (Exception exception)
        {
            // Telemetry is optional. A registration/API mismatch must never
            // prevent the character mod itself from loading.
            Entry.Logger.Warn(
                $"MGR telemetry run-save registration failed; uploads will be disabled: {exception}");
        }
        RitsuLibFramework.RegisterTelemetryApplicant(
            new TelemetryApplicant
            {
                ApplicantId = Entry.ModId,
                OwnerModId = Entry.ModId,
                DisplayName = "MGR",
                DisplayNameText = ModSettingsText.LocString(
                    "settings_ui",
                    "MGR_MOD_SETTINGS_UI_TELEMETRY_MOD_NAME",
                    "MGR Mod"),
                Adapter = CreateAdapter(),
                Requests =
                [
                    TelemetryRequest.Custom(
                        CleanRunRequestId,
                        ModSettingsText.LocString(
                            "settings_ui",
                            "MGR_MOD_SETTINGS_UI_TELEMETRY_RUN_HISTORY",
                            "Send sanitized single-player MGR balance data."))
                ]
            });

        _client = TelemetryApi.GetClient(Entry.ModId);
        MgrLoadoutUsageTracker.Register();
        _runStartedSubscription = RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(
            evt =>
            {
                MgrLoadoutUsageTracker.BeginRun();
                MgrRunTelemetryAccumulator.BeginRun(evt.RunState, isNewRun: true);
            },
            replayCurrentState: false);
        _runLoadedSubscription = RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(
            evt =>
            {
                MgrLoadoutUsageTracker.BeginRun();
                MgrRunTelemetryAccumulator.BeginRun(evt.RunState, isNewRun: false);
            },
            replayCurrentState: false);
        _runEndedSubscription = RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
            CaptureCleanRun,
            replayCurrentState: false);
    }

    private static void CaptureCleanRun(RunEndedEvent evt)
    {
        try
        {
            ITelemetryClient? client = _client;
            if (client is null)
            {
                Entry.Logger.Info("MGR telemetry skipped: the RitsuLib telemetry client is unavailable");
                return;
            }

            if (!client.IsEnabled(CleanRunRequestId))
            {
                Entry.Logger.Info(
                    $"MGR telemetry skipped: the RitsuLib request '{CleanRunRequestId}' is not authorized");
                return;
            }

            if (!MgrTelemetryEligibility.ShouldUpload(evt, out string rejectionReason))
            {
                Entry.Logger.Info($"MGR telemetry skipped: {rejectionReason}");
                return;
            }

            if (!MgrTelemetryIdentity.TryGet(
                    evt.Run,
                    out MgrTelemetryIdentityInfo identity,
                    out rejectionReason))
            {
                Entry.Logger.Info($"MGR telemetry skipped: {rejectionReason}");
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!MgrTelemetryIdentity.CanSubmit(identity, now, out rejectionReason))
            {
                Entry.Logger.Info($"MGR telemetry skipped: {rejectionReason}");
                return;
            }

            if (!MgrRunSanityValidator.IsValidSource(evt, out rejectionReason))
            {
                Entry.Logger.Info($"MGR telemetry skipped: {rejectionReason}");
                return;
            }

            string eventId = MgrTelemetryIdentity.BuildEventId(evt.Run, identity);
            MgrRunMetrics metrics = MgrRunMetricsBuilder.Build(evt, identity, eventId);
            if (!MgrRunSanityValidator.IsValidPayload(metrics, out rejectionReason))
            {
                Entry.Logger.Info($"MGR telemetry skipped: {rejectionReason}");
                return;
            }

            client.CapturePayload(
                CleanRunEventName,
                CleanRunRequestId,
                metrics.Payload,
                metrics.IndexedProperties);
            MgrTelemetryIdentity.MarkSubmitted(identity, now);
        }
        catch (Exception exception)
        {
            // Telemetry must never interfere with run completion. Do not use the
            // diagnostics channel here: it was not part of the player's consent.
            Entry.Logger.Warn($"Failed to build MGR telemetry payload: {exception}");
        }
        finally
        {
            MgrLoadoutUsageTracker.FinishRun(evt.Run);
            MgrRunTelemetryAccumulator.Reset();
        }
    }

    private static ITelemetryAdapter CreateAdapter()
    {
        if (string.IsNullOrWhiteSpace(PostHogHost)
            || string.IsNullOrWhiteSpace(PostHogProjectApiKey))
        {
            return new DisabledTelemetryAdapter(
                "MGR telemetry is ready, but its cloud endpoint has not been configured.");
        }

        return new MgrSanitizedPostHogTelemetryAdapter(
            host: PostHogHost,
            projectApiKey: PostHogProjectApiKey);
    }
}

internal static class MgrTelemetryEligibility
{
    private const int MinimumMapPoints = 5;
    private const int MinimumAbandonedMapPoints = 10;
    private const long MinimumAbandonedDurationSeconds = 5L * 60L;
    private const long MinimumVictoryDurationSeconds = 20L * 60L;

    public static bool ShouldUpload(RunEndedEvent evt, out string reason)
    {
        if (!SaveManager.Instance.PrefsSave.UploadData)
            return Reject("the base-game upload-data setting is disabled", out reason);

        if (SaveManager.Instance.SettingsSave.FullConsole)
            return Reject("full console is enabled", out reason);

        if (TestMode.IsOn || Godot.OS.HasFeature("editor"))
            return Reject("the game is running in a test/editor environment", out reason);

        if (ReleaseInfoManager.Instance.ReleaseInfo is null)
            return Reject("the game is not a release build", out reason);

        if (SaveManager.Instance.Progress.NumberOfRuns <= 3)
            return Reject("the profile's first three recorded runs are excluded", out reason);

        if (evt.Run.GameMode != MegaCrit.Sts2.Core.Runs.GameMode.Standard)
            return Reject("only standard-mode runs are accepted", out reason);

        if (evt.Run.Players.Count != 1)
            return Reject("only single-player runs are accepted", out reason);

        if (evt.Run.Players[0].CharacterId !=
            MegaCrit.Sts2.Core.Models.ModelDb.Character<Characters.MgrCharacter>().Id)
        {
            return Reject("the single player is not MGR", out reason);
        }

        int floorReached = evt.Run.MapPointHistory.Sum(act => act.Count);
        if (!evt.IsAbandoned && floorReached < MinimumMapPoints)
            return Reject($"the run reached only {floorReached} map points", out reason);

        long durationSeconds = GetDurationSeconds(evt);
        if (durationSeconds <= 0)
            return Reject("the run has no valid duration", out reason);

        if (evt.IsAbandoned
            && durationSeconds < MinimumAbandonedDurationSeconds
            && floorReached < MinimumAbandonedMapPoints)
        {
            return Reject(
                $"the abandoned run lasted only {durationSeconds} seconds and reached only {floorReached} map points",
                out reason);
        }

        if (evt.IsVictory && durationSeconds < MinimumVictoryDurationSeconds)
        {
            return Reject(
                $"the victorious run lasted only {durationSeconds} seconds",
                out reason);
        }

        if (MgrLoadoutUsageTracker.WasUsedInRun(evt.Run))
            return Reject("Loadout modified this run", out reason);

        reason = string.Empty;
        return true;
    }

    internal static long GetDurationSeconds(RunEndedEvent evt) =>
        evt.IsVictory && evt.Run.WinTime > 0
            ? evt.Run.WinTime
            : evt.Run.RunTime;

    private static bool Reject(string rejectionReason, out string reason)
    {
        reason = rejectionReason;
        return false;
    }
}
