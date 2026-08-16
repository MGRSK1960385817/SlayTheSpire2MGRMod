[CmdletBinding()]
param(
    [string]$Path = '',
    [int]$MinimumSchemaVersion = 7
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Path)) {
    $latest = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Data') `
        -Filter 'mgr_run_completed_*.json' -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw 'No downloaded mgr_run_completed JSON file was found.'
    }
    $Path = $latest.FullName
}

$document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
$results = foreach ($eventRecord in @($document.events)) {
    $payload = $eventRecord.payload
    $issues = [System.Collections.Generic.List[string]]::new()

    if ([int]$payload.schema_version -lt $MinimumSchemaVersion) {
        $issues.Add("schema $($payload.schema_version) predates required schema $MinimumSchemaVersion")
    }

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

    [pscustomobject]@{
        EventId          = $payload.event_id
        SchemaVersion    = $payload.schema_version
        FloorReached     = $payload.floor_reached
        ReloadCount      = $payload.reload_count
        TrackingComplete = $mechanics.tracking_complete
        IssueCount       = $issues.Count
        Issues           = $issues -join '; '
    }
}

$results | Format-Table -AutoSize -Wrap
$issueCount = @($results | Where-Object IssueCount -gt 0).Count
if ($issueCount -gt 0) {
    throw "$issueCount telemetry record(s) failed consistency checks."
}

Write-Host "Validated $(@($results).Count) telemetry record(s)."
