[CmdletBinding()]
param(
    [int]$ProjectId = 560344,
    [ValidateSet('https://us.posthog.com', 'https://eu.posthog.com')]
    [string]$PostHogHost = 'https://us.posthog.com',
    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string]$EventName = 'mgr_run_completed',
    [ValidateRange(1, 100000)]
    [int]$Limit = 10000,
    [string]$OutputDirectory = '',
    [string]$PersonalApiKey = $env:POSTHOG_PERSONAL_API_KEY
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PersonalApiKey)) {
    $secureKey = Read-Host 'PostHog Personal API key' -AsSecureString
    $credential = [System.Management.Automation.PSCredential]::new('posthog', $secureKey)
    $PersonalApiKey = $credential.GetNetworkCredential().Password
}

if ([string]::IsNullOrWhiteSpace($PersonalApiKey)) {
    throw 'A PostHog Personal API key is required.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    # Raw telemetry remains inside the requested DataAnalysis workspace, but
    # its Data subfolder is ignored by Git and excluded from Godot exports.
    $OutputDirectory = Join-Path $PSScriptRoot 'Data'
}

$query = @"
SELECT
    uuid,
    timestamp,
    event,
    properties
FROM events
WHERE event = '$EventName'
  AND properties.request_id = 'mgr_clean_run_metrics'
ORDER BY timestamp DESC
LIMIT $Limit
"@

$requestBody = @{
    query = @{
        kind  = 'HogQLQuery'
        query = $query
    }
} | ConvertTo-Json -Depth 5

$headers = @{
    Authorization = "Bearer $PersonalApiKey"
}

$endpoint = "$($PostHogHost.TrimEnd('/'))/api/projects/$ProjectId/query/"
$response = Invoke-RestMethod `
    -Method Post `
    -Uri $endpoint `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body $requestBody

$columns = @($response.columns)
$rawEvents = foreach ($row in @($response.results)) {
    $record = [ordered]@{}
    for ($index = 0; $index -lt $columns.Count; $index++) {
        $record[[string]$columns[$index]] = $row[$index]
    }
    [pscustomobject]$record
}

# Store only analysis fields. PostHog may enrich an event from its request IP;
# retain one country and city value, while dropping the raw IP, coordinates,
# postal data, person-profile copies, generic runtime diagnostics and empty
# columns from the local dataset.
$events = foreach ($record in @($rawEvents)) {
    $properties = $record.properties
    if ($properties -is [string]) {
        $properties = $properties | ConvertFrom-Json -Depth 100
    }

    $applicantPayload = $properties.payload.applicant_payload
    if ($null -eq $applicantPayload) {
        continue
    }

    [pscustomobject][ordered]@{
        uuid       = $record.uuid
        timestamp  = $record.timestamp
        event      = $record.event
        request_id = $properties.request_id
        country    = $properties.'$geoip_country_name'
        city       = $properties.'$geoip_city_name'
        payload    = $applicantPayload
    }
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
$safeEventName = $EventName -replace '[^A-Za-z0-9_.-]', '_'
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd_HHmmss')
$outputPath = Join-Path $resolvedOutputDirectory "$safeEventName`_$timestamp.json"

$download = [ordered]@{
    downloaded_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    project_id        = $ProjectId
    host              = $PostHogHost
    event_name        = $EventName
    count             = @($events).Count
    columns           = @('uuid', 'timestamp', 'event', 'request_id', 'country', 'city', 'payload')
    events            = @($events)
}

$download | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $outputPath -Encoding utf8

# Never print the key or request headers. Return only safe download metadata.
[pscustomobject]@{
    EventName = $EventName
    Count     = @($events).Count
    Output    = $outputPath
}
