using STS2RitsuLib.Telemetry;

namespace MGRMod.Telemetry;

/// <summary>
/// Removes RitsuLib's generic runtime diagnostics before MGR events leave the
/// client. Consent, queuing and retries remain owned by RitsuLib; only the
/// final PostHog envelope is reduced to MGR's explicit analysis allow-list.
/// </summary>
internal sealed class MgrSanitizedPostHogTelemetryAdapter : ITelemetryAdapter
{
    private static readonly HashSet<string> AllowedProperties =
    [
        // Required by the PostHog adapter as its anonymous distinct ID.
        "anonymous_install_id",

        // MGR's deliberately indexed run fields.
        "schema_version",
        "event_id",
        "install_id",
        "steam_id",
        "mod_version",
        "victory",
        "ascension",
        "floor_reached",
        "duration_seconds",
        "reload_count",

        // Useful for localization coverage without retaining OS/runtime data.
        "game_language"
    ];

    private readonly PostHogTelemetryAdapter _inner;

    public MgrSanitizedPostHogTelemetryAdapter(string host, string projectApiKey)
    {
        _inner = new PostHogTelemetryAdapter(host, projectApiKey);
    }

    public string AdapterId => _inner.AdapterId;

    public string EndpointDescription => _inner.EndpointDescription;

    public ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken)
    {
        List<TelemetryEnvelope> sanitized = new(events.Count);
        foreach (TelemetryEnvelope envelope in events)
        {
            Dictionary<string, object?> properties = envelope.Properties
                .Where(pair => AllowedProperties.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            // MGR needs events, not PostHog person profiles. This also avoids
            // duplicating event metadata into $set/$set_once person records.
            properties["$process_person_profile"] = false;

            sanitized.Add(new TelemetryEnvelope
            {
                Schema = envelope.Schema,
                ApplicantId = envelope.ApplicantId,
                EventName = envelope.EventName,
                RequestId = envelope.RequestId,
                Category = envelope.Category,
                TimestampUtc = envelope.TimestampUtc,
                Properties = properties,
                Payload = envelope.Payload
            });
        }

        return _inner.SendAsync(applicant, sanitized, cancellationToken);
    }
}
