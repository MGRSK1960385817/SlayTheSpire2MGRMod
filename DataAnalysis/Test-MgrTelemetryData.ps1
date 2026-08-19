[CmdletBinding()]
param(
    [string]$Path = '',
    [int]$MinimumSchemaVersion = 7,
    [switch]$FailOnLegacySchema,
    [switch]$ShowAll
)

$ErrorActionPreference = 'Stop'

function Read-TelemetryEvents {
    param([Parameter(Mandatory)][string]$InputPath)

    if ($InputPath.EndsWith('.jsonl.gz', [System.StringComparison]::OrdinalIgnoreCase)) {
        $fileStream = $null
        $gzipStream = $null
        $reader = $null
        try {
            $fileStream = [System.IO.File]::OpenRead($InputPath)
            $gzipStream = [System.IO.Compression.GZipStream]::new(
                $fileStream,
                [System.IO.Compression.CompressionMode]::Decompress,
                $false)
            $reader = [System.IO.StreamReader]::new($gzipStream, [System.Text.Encoding]::UTF8)
            while (-not $reader.EndOfStream) {
                $line = $reader.ReadLine()
                if (-not [string]::IsNullOrWhiteSpace($line)) {
                    Write-Output ($line | ConvertFrom-Json -Depth 100)
                }
            }
        } finally {
            if ($null -ne $reader) {
                $reader.Dispose()
            } elseif ($null -ne $gzipStream) {
                $gzipStream.Dispose()
            } elseif ($null -ne $fileStream) {
                $fileStream.Dispose()
            }
        }
        return
    }

    if ($InputPath.EndsWith('.jsonl', [System.StringComparison]::OrdinalIgnoreCase)) {
        foreach ($line in [System.IO.File]::ReadLines($InputPath)) {
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                Write-Output ($line | ConvertFrom-Json -Depth 100)
            }
        }
        return
    }

    if ($InputPath.EndsWith('.json', [System.StringComparison]::OrdinalIgnoreCase)) {
        $document = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json -Depth 100
        foreach ($eventRecord in @($document.events)) {
            Write-Output $eventRecord
        }
        return
    }

    throw "Unsupported telemetry file format: $InputPath"
}

if ([string]::IsNullOrWhiteSpace($Path)) {
    $latest = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Data') `
        -Filter 'mgr_run_completed_*' -File |
        Where-Object {
            $_.Name -notlike '*.manifest.json' -and
            $_.Name -match '\.(json|jsonl|jsonl\.gz)$'
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw 'No downloaded mgr_run_completed telemetry file was found.'
    }
    $Path = $latest.FullName
}

$resolvedPath = [System.IO.Path]::GetFullPath($Path)
$results = [System.Collections.Generic.List[object]]::new()
$validatedCount = 0
$legacySkippedCount = 0

foreach ($eventRecord in (Read-TelemetryEvents -InputPath $resolvedPath)) {
    $payload = $eventRecord.payload
    $issues = [System.Collections.Generic.List[string]]::new()

    if ([int]$payload.schema_version -lt $MinimumSchemaVersion) {
        if (-not $FailOnLegacySchema) {
            $legacySkippedCount++
            continue
        }
        $issues.Add("schema $($payload.schema_version) predates required schema $MinimumSchemaVersion")
    }
    $validatedCount++

    $mechanics = $payload.mgr_mechanics
    $noteSum = 0L
    foreach ($property in @($mechanics.notes_by_kind.PSObject.Properties)) {
        $noteSum += [long]$property.Value
    }
    if ($noteSum -ne [long]$mechanics.notes_generated) {
        $issues.Add("note kinds sum to $noteSum, total is $($mechanics.notes_generated)")
    }

    $damageSum = 0L
    foreach ($name in @('card', 'note', 'other', 'unclassified')) {
        $damageSum += [long]$mechanics.damage_by_source.$name
    }
    if ($damageSum -ne [long]$payload.final_player.damage_dealt) {
        $issues.Add("damage sources sum to $damageSum, base total is $($payload.final_player.damage_dealt)")
    }

    $floors = @($payload.floors)
    for ($index = 0; $index -lt $floors.Count; $index++) {
        $floor = $floors[$index]
        if ([int]$floor.floor -ne $index + 1) {
            $issues.Add("floor sequence breaks at array index $index")
        }
        foreach ($card in @($floor.cards_gained)) {
            if ($null -eq $card.floor_added) {
                $issues.Add("floor $($floor.floor) gained card $($card.id) has no floor_added")
            }
        }
    }

    if ($floors.Count -gt 0 -and [int]$floors[0].hp_healed -ne 0) {
        $issues.Add("initial setup node reports $($floors[0].hp_healed) healing")
    }
    if ($floors.Count -gt 0) {
        $lastFloor = $floors[-1]
        if ([int]$lastFloor.current_hp -ne [int]$payload.final_player.current_hp) {
            $issues.Add("last floor HP does not match final player HP")
        }
    }

    if ($ShowAll -or $issues.Count -gt 0) {
        $results.Add([pscustomobject]@{
            EventId          = $payload.event_id
            SchemaVersion    = $payload.schema_version
            FloorReached     = $payload.floor_reached
            ReloadCount      = $payload.reload_count
            TrackingComplete = $mechanics.tracking_complete
            IssueCount       = $issues.Count
            Issues           = $issues -join '; '
        })
    }
}

if ($results.Count -gt 0) {
    $results | Format-Table -AutoSize -Wrap
}
$issueCount = @($results | Where-Object IssueCount -gt 0).Count
if ($issueCount -gt 0) {
    throw "$issueCount telemetry record(s) failed consistency checks."
}

Write-Host "Validated $validatedCount telemetry record(s) from $resolvedPath."
if ($legacySkippedCount -gt 0) {
    Write-Host "Skipped $legacySkippedCount record(s) older than schema $MinimumSchemaVersion. Use -FailOnLegacySchema to reject them instead."
}
