using System.Reflection;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Telemetry;
using SlayTheSpire2MGRMod.Characters;

namespace SlayTheSpire2MGRMod.Telemetry;

/// <summary>
/// Registers MGR's opt-in run-history telemetry with RitsuLib.
/// RitsuLib owns consent, capture, persistence, retries and network-failure isolation.
/// </summary>
public static class MgrTelemetry
{
    private const string RunContextContributionId = "mgr_run_context";

    // Configure an MGR-owned PostHog ingestion endpoint before publishing telemetry.
    // Never place a PostHog personal/admin key here: only a project ingestion key or
    // a restricted proxy token is appropriate for a client-side mod.
    private const string PostHogHost = "";
    private const string PostHogProjectApiKey = "";

    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        RitsuLibFramework.RegisterTelemetryContributionProvider(new MgrRunContextProvider());
        RitsuLibFramework.RegisterTelemetryApplicant(
            new TelemetryApplicant
            {
                ApplicantId = Entry.ModId,
                OwnerModId = Entry.ModId,
                DisplayName = "MGR",
                DisplayNameText = ModSettingsText.LocString(
                    "settings_ui",
                    "SLAY_THE_SPIRE2_MGR_MOD_SETTINGS_UI_TELEMETRY_MOD_NAME",
                    "MGR Mod"),
                Adapter = CreateAdapter(),
                Requests =
                [
                    TelemetryRequest.RunHistory(
                        ModSettingsText.LocString(
                            "settings_ui",
                            "SLAY_THE_SPIRE2_MGR_MOD_SETTINGS_UI_TELEMETRY_RUN_HISTORY",
                            "Send completed MGR run history for balance analysis."),
                        sharedContributionSubscriptions: [RunContextContributionId],
                        captureFilter: evt =>
                            !evt.IsAbandoned
                            && evt.Run.Players.Any(player =>
                                player.CharacterId == ModelDb.Character<MgrCharacter>().Id))
                ]
            });
    }

    private static ITelemetryAdapter CreateAdapter()
    {
        if (string.IsNullOrWhiteSpace(PostHogHost)
            || string.IsNullOrWhiteSpace(PostHogProjectApiKey))
        {
            return new DisabledTelemetryAdapter(
                "MGR telemetry is ready, but its cloud endpoint has not been configured.");
        }

        return new PostHogTelemetryAdapter(
            host: PostHogHost,
            projectApiKey: PostHogProjectApiKey);
    }

    private sealed class MgrRunContextProvider : ITelemetryContributionProvider
    {
        public string ContributorModId => Entry.ModId;
        public string ContributionId => RunContextContributionId;
        public TelemetryDataCategory Category => TelemetryDataCategory.RunHistory;
        public TelemetryContributionVisibility Visibility =>
            TelemetryContributionVisibility.PrivateToApplicant;

        public JsonNode Build(TelemetryContributionContext context)
        {
            return new JsonObject
            {
                ["schema_version"] = 1,
                ["mod_version"] = GetModVersion()
            };
        }
    }

    private static string GetModVersion()
    {
        Assembly assembly = typeof(MgrTelemetry).Assembly;
        return assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
