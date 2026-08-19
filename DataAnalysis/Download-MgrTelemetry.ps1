[CmdletBinding()]
param(
    [int]$ProjectId = 560344,
    [ValidateSet('https://us.posthog.com', 'https://eu.posthog.com')]
    [string]$PostHogHost = 'https://us.posthog.com',
    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string]$EventName = 'mgr_run_completed',
    [ValidateRange(100, 5000)]
    [int]$PageSize = 1000,
    [Alias('Limit')]
    [ValidateRange(0, 100000)]
    [int]$MaxEvents = 0,
    [Nullable[DateTimeOffset]]$SinceUtc,
    [Nullable[DateTimeOffset]]$UntilUtc,
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

if ($SinceUtc.HasValue -and $UntilUtc.HasValue -and $SinceUtc.Value -gt $UntilUtc.Value) {
    throw 'SinceUtc must not be later than UntilUtc.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    # Raw telemetry remains inside the requested DataAnalysis workspace, but
    # its Data subfolder is ignored by Git and excluded from Godot exports.
    $OutputDirectory = Join-Path $PSScriptRoot 'Data'
}

function ConvertTo-HogQlDateTime64 {
    param([Parameter(Mandatory)][DateTimeOffset]$Value)

    $utcText = $Value.ToUniversalTime().ToString(
        'yyyy-MM-dd HH:mm:ss.ffffff',
        [System.Globalization.CultureInfo]::InvariantCulture)
    return "toDateTime64('$utcText', 6, 'UTC')"
}

function ConvertTo-AnalysisEvent {
    param([Parameter(Mandatory)]$Record)

    $properties = $Record.properties
    if ($properties -is [string]) {
        $properties = $properties | ConvertFrom-Json -Depth 100
    }

    $applicantPayload = $properties.payload.applicant_payload
    if ($null -eq $applicantPayload) {
        return $null
    }

    return [pscustomobject][ordered]@{
        uuid       = [string]$Record.uuid
        timestamp  = $Record.timestamp
        event      = [string]$Record.event
        request_id = [string]$properties.request_id
        country    = $properties.'$geoip_country_name'
        city       = $properties.'$geoip_city_name'
        payload    = $applicantPayload
    }
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
$safeEventName = $EventName -replace '[^A-Za-z0-9_.-]', '_'
$downloadStamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd_HHmmss')
$outputPath = Join-Path $resolvedOutputDirectory "$safeEventName`_$downloadStamp.jsonl.gz"
$partialPath = "$outputPath.partial"
$manifestPath = Join-Path $resolvedOutputDirectory "$safeEventName`_$downloadStamp.manifest.json"

# Freeze the upper edge of this snapshot so events arriving during the
# download cannot move already-read pages. A caller-provided UntilUtc takes
# precedence and also makes historical range exports reproducible.
$snapshotUntil = if ($UntilUtc.HasValue) {
    $UntilUtc.Value.ToUniversalTime()
} else {
    [DateTimeOffset]::UtcNow
}

$headers = @{ Authorization = "Bearer $PersonalApiKey" }
$endpoint = "$($PostHogHost.TrimEnd('/'))/api/projects/$ProjectId/query/"
$seenEventIds = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
$seenFallbackUuids = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)

$pageCount = 0
$rawRowCount = 0
$writtenCount = 0
$duplicateCount = 0
$missingPayloadCount = 0
$earliestTimestamp = $null
$latestTimestamp = $null
$cursorTimestamp = $null
$cursorUuid = $null
$completed = $false

$fileStream = $null
$gzipStream = $null
$writer = $null

try {
    $fileStream = [System.IO.File]::Open(
        $partialPath,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $gzipStream = [System.IO.Compression.GZipStream]::new(
        $fileStream,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    $writer = [System.IO.StreamWriter]::new($gzipStream, $utf8WithoutBom, 65536)

    while ($true) {
        $remaining = if ($MaxEvents -gt 0) { $MaxEvents - $writtenCount } else { $PageSize }
        if ($MaxEvents -gt 0 -and $remaining -le 0) {
            break
        }
        $queryLimit = [Math]::Min($PageSize, [Math]::Max(1, $remaining))

        $whereClauses = [System.Collections.Generic.List[string]]::new()
        $whereClauses.Add("event = '$EventName'")
        $whereClauses.Add("properties.request_id = 'mgr_clean_run_metrics'")
        $whereClauses.Add("timestamp <= $(ConvertTo-HogQlDateTime64 $snapshotUntil)")
        if ($SinceUtc.HasValue) {
            $whereClauses.Add("timestamp >= $(ConvertTo-HogQlDateTime64 $SinceUtc.Value)")
        }
        if ($null -ne $cursorTimestamp) {
            $cursorTimeExpression = ConvertTo-HogQlDateTime64 $cursorTimestamp
            $whereClauses.Add(
                "(timestamp < $cursorTimeExpression OR (timestamp = $cursorTimeExpression AND uuid < toUUID('$cursorUuid')))"
            )
        }

        $whereSql = $whereClauses -join "`n  AND "
        $query = @"
SELECT
    uuid,
    timestamp,
    event,
    properties
FROM events
WHERE $whereSql
ORDER BY timestamp DESC, uuid DESC
LIMIT $queryLimit
"@

        $requestBody = @{
            query = @{
                kind  = 'HogQLQuery'
                query = $query
            }
        } | ConvertTo-Json -Depth 5

        $response = Invoke-RestMethod `
            -Method Post `
            -Uri $endpoint `
            -Headers $headers `
            -ContentType 'application/json' `
            -Body $requestBody

        $columns = @($response.columns)
        $rows = @($response.results)
        if ($rows.Count -eq 0) {
            break
        }

        $pageCount++
        $rawRowCount += $rows.Count
        foreach ($row in $rows) {
            $record = [ordered]@{}
            for ($index = 0; $index -lt $columns.Count; $index++) {
                $record[[string]$columns[$index]] = $row[$index]
            }
            $record = [pscustomobject]$record

            $rowTimestamp = [DateTimeOffset]::Parse(
                [string]$record.timestamp,
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::AssumeUniversal)
            $rowUuid = [string]$record.uuid
            $cursorTimestamp = $rowTimestamp
            $cursorUuid = $rowUuid

            $analysisEvent = ConvertTo-AnalysisEvent $record
            if ($null -eq $analysisEvent) {
                $missingPayloadCount++
                continue
            }

            $eventId = [string]$analysisEvent.payload.event_id
            $isNew = if ([string]::IsNullOrWhiteSpace($eventId)) {
                $seenFallbackUuids.Add($rowUuid)
            } else {
                $seenEventIds.Add($eventId)
            }
            if (-not $isNew) {
                $duplicateCount++
                continue
            }

            $writer.WriteLine(($analysisEvent | ConvertTo-Json -Depth 100 -Compress))
            $writtenCount++
            if ($null -eq $latestTimestamp -or $rowTimestamp -gt $latestTimestamp) {
                $latestTimestamp = $rowTimestamp
            }
            if ($null -eq $earliestTimestamp -or $rowTimestamp -lt $earliestTimestamp) {
                $earliestTimestamp = $rowTimestamp
            }

            if ($MaxEvents -gt 0 -and $writtenCount -ge $MaxEvents) {
                break
            }
        }

        Write-Progress `
            -Activity 'Downloading MGR telemetry' `
            -Status "Pages: $pageCount; unique events: $writtenCount" `
            -PercentComplete -1

        if ($rows.Count -lt $queryLimit -or ($MaxEvents -gt 0 -and $writtenCount -ge $MaxEvents)) {
            break
        }
    }

    $writer.Flush()
    $writer.Dispose()
    $writer = $null
    $gzipStream = $null
    $fileStream = $null

    Move-Item -LiteralPath $partialPath -Destination $outputPath -Force
    $sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
    $archiveLength = (Get-Item -LiteralPath $outputPath).Length

    $manifest = [ordered]@{
        format                   = 'mgr-telemetry-jsonl-gzip-v1'
        downloaded_at_utc        = [DateTimeOffset]::UtcNow.ToString('O')
        project_id               = $ProjectId
        host                     = $PostHogHost
        event_name               = $EventName
        request_id               = 'mgr_clean_run_metrics'
        snapshot_until_utc       = $snapshotUntil.ToString('O')
        requested_since_utc      = if ($SinceUtc.HasValue) { $SinceUtc.Value.ToUniversalTime().ToString('O') } else { $null }
        max_events               = $MaxEvents
        page_size                = $PageSize
        pages                    = $pageCount
        raw_rows_downloaded      = $rawRowCount
        unique_events_written    = $writtenCount
        duplicate_events_skipped = $duplicateCount
        missing_payload_skipped  = $missingPayloadCount
        earliest_event_utc       = if ($null -ne $earliestTimestamp) { $earliestTimestamp.ToUniversalTime().ToString('O') } else { $null }
        latest_event_utc         = if ($null -ne $latestTimestamp) { $latestTimestamp.ToUniversalTime().ToString('O') } else { $null }
        archive_file             = [System.IO.Path]::GetFileName($outputPath)
        archive_bytes            = $archiveLength
        archive_sha256           = $sha256
        columns                  = @('uuid', 'timestamp', 'event', 'request_id', 'country', 'city', 'payload')
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $completed = $true
} finally {
    Write-Progress -Activity 'Downloading MGR telemetry' -Completed
    if ($null -ne $writer) {
        $writer.Dispose()
    } elseif ($null -ne $gzipStream) {
        $gzipStream.Dispose()
    } elseif ($null -ne $fileStream) {
        $fileStream.Dispose()
    }
    if (-not $completed -and (Test-Path -LiteralPath $partialPath)) {
        Remove-Item -LiteralPath $partialPath -Force
    }
}

# Never print the key or request headers. Return only safe download metadata.
[pscustomobject]@{
    EventName = $EventName
    Count     = $writtenCount
    Pages     = $pageCount
    Output    = $outputPath
    Manifest  = $manifestPath
}
