[CmdletBinding()]
param(
    [switch]$WarningsAsErrors
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-ValidationError([string]$Message) {
    $script:errors.Add($Message)
}

function Add-ValidationWarning([string]$Message) {
    $script:warnings.Add($Message)
}

function Read-JsonHashtable([string]$Path) {
    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 |
            ConvertFrom-Json -AsHashtable
    }
    catch {
        Add-ValidationError "Invalid JSON: $Path ($($_.Exception.Message))"
        return @{}
    }
}

function Find-SourceFile([string]$Category, [string]$CodeName) {
    $matches = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot "Scripts/$Category") `
        -Recurse -File -Filter "$CodeName.cs")
    if ($matches.Count -ne 1) {
        Add-ValidationError "$Category '$CodeName' should have exactly one source file; found $($matches.Count)."
        return $null
    }

    return $matches[0]
}

function Test-RequiredFile([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Add-ValidationError "Missing ${Label}: $Path"
        return $false
    }

    return $true
}

function Get-StableStem([System.IO.FileInfo]$Source, [string]$Kind, [string]$CodeName) {
    if ($null -eq $Source) { return $null }
    $text = Get-Content -LiteralPath $Source.FullName -Raw -Encoding UTF8
    $match = [regex]::Match($text, 'StableEntryStem\s*=\s*"([^"]+)"')
    if (-not $match.Success) {
        Add-ValidationError "$Kind '$CodeName' has no explicit StableEntryStem in $($Source.FullName)."
        return $null
    }

    return $match.Groups[1].Value
}

function Get-DeclaredAssetPaths([System.IO.FileInfo]$Source, [string]$AssetFolder) {
    if ($null -eq $Source) { return @() }

    $text = Get-Content -LiteralPath $Source.FullName -Raw -Encoding UTF8
    $matches = [regex]::Matches(
        $text,
        "images/$AssetFolder/([A-Za-z0-9_+.-]+\.(?:png|jpg|jpeg|webp|svg))",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    return @($matches | ForEach-Object {
        "SlayTheSpire2MGRMod/images/$AssetFolder/$($_.Groups[1].Value)"
    } | Sort-Object -Unique)
}

function Test-ContentAssets(
    [System.IO.FileInfo]$Source,
    [string]$AssetFolder,
    [string[]]$DefaultRelativePaths,
    [string]$Label
) {
    $declaredPaths = @(Get-DeclaredAssetPaths $Source $AssetFolder)
    $paths = if ($declaredPaths.Count -gt 0) {
        $declaredPaths
    }
    else {
        $DefaultRelativePaths
    }

    foreach ($relativePath in $paths) {
        $assetPath = Join-Path $repoRoot $relativePath
        Test-RequiredFile $assetPath "$Label asset" | Out-Null
        Test-RequiredFile "$assetPath.import" "$Label Godot import" | Out-Null
    }
}

function Test-LocalizationEntry(
    [hashtable]$Localization,
    [string]$Prefix,
    [string]$Language,
    [string]$Kind,
    [string]$CodeName,
    [string]$ExpectedChineseName
) {
    $titleKey = "$Prefix.title"
    $descriptionKey = "$Prefix.description"
    if (-not $Localization.ContainsKey($titleKey)) {
        Add-ValidationError "Missing $Language title for $Kind '$CodeName': $titleKey"
    }
    elseif ($Language -eq 'zhs' -and $Localization[$titleKey] -ne $ExpectedChineseName) {
        Add-ValidationWarning "Chinese title differs from registry for $Kind '$CodeName': registry='$ExpectedChineseName', localization='$($Localization[$titleKey])'."
    }

    if (-not $Localization.ContainsKey($descriptionKey)) {
        Add-ValidationError "Missing $Language description for $Kind '$CodeName': $descriptionKey"
    }
}

$registryPath = Join-Path $repoRoot 'docs/MGR_content_registry.json'
$registry = Read-JsonHashtable $registryPath
$zhsCards = Read-JsonHashtable (Join-Path $repoRoot 'SlayTheSpire2MGRMod/localization/zhs/cards.json')
$engCards = Read-JsonHashtable (Join-Path $repoRoot 'SlayTheSpire2MGRMod/localization/eng/cards.json')
$zhsRelics = Read-JsonHashtable (Join-Path $repoRoot 'SlayTheSpire2MGRMod/localization/zhs/relics.json')
$engRelics = Read-JsonHashtable (Join-Path $repoRoot 'SlayTheSpire2MGRMod/localization/eng/relics.json')

$activeCards = @($registry.cards | Where-Object { [int]$_.status -eq 1 })
$activeRelics = @($registry.relics | Where-Object { [int]$_.status -eq 1 })

$duplicateCardCodes = @($activeCards | Group-Object codeName | Where-Object Count -gt 1)
foreach ($duplicate in $duplicateCardCodes) {
    Add-ValidationError "Duplicate active card codeName: $($duplicate.Name)"
}
$duplicateRelicCodes = @($activeRelics | Group-Object codeName | Where-Object Count -gt 1)
foreach ($duplicate in $duplicateRelicCodes) {
    Add-ValidationError "Duplicate active relic codeName: $($duplicate.Name)"
}

foreach ($card in $activeCards) {
    $codeName = [string]$card.codeName
    $source = Find-SourceFile 'Cards' $codeName
    $stem = Get-StableStem $source 'card' $codeName

    Test-ContentAssets `
        $source `
        'cards' `
        @("SlayTheSpire2MGRMod/images/cards/$codeName.png") `
        "card '$codeName'"

    if ($stem) {
        $prefix = 'SLAY_THE_SPIRE2_MGR_MOD_CARD_' + $stem.ToUpperInvariant()
        Test-LocalizationEntry $zhsCards $prefix 'zhs' 'card' $codeName ([string]$card.name)
        Test-LocalizationEntry $engCards $prefix 'eng' 'card' $codeName ([string]$card.name)
    }
}

foreach ($relic in $activeRelics) {
    $codeName = [string]$relic.codeName
    $source = Find-SourceFile 'Relics' $codeName
    $stem = Get-StableStem $source 'relic' $codeName

    Test-ContentAssets `
        $source `
        'relics' `
        @(
            "SlayTheSpire2MGRMod/images/relics/$codeName.png",
            "SlayTheSpire2MGRMod/images/relics/${codeName}_outline.png"
        ) `
        "relic '$codeName'"

    if ($stem) {
        $prefix = 'SLAY_THE_SPIRE2_MGR_MOD_RELIC_' + $stem.ToUpperInvariant()
        Test-LocalizationEntry $zhsRelics $prefix 'zhs' 'relic' $codeName ([string]$relic.name)
        Test-LocalizationEntry $engRelics $prefix 'eng' 'relic' $codeName ([string]$relic.name)
    }
}

# Every registered content model should be represented in the human registry.
$activeCardCodeSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@($activeCards.codeName),
    [System.StringComparer]::Ordinal)
$registeredCardFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Scripts/Cards') `
    -Recurse -File -Filter '*.cs' | Where-Object {
        Select-String -LiteralPath $_.FullName -Pattern '\[RegisterCard' -Quiet
    }
foreach ($source in $registeredCardFiles) {
    if (-not $activeCardCodeSet.Contains($source.BaseName)) {
        Add-ValidationWarning "Registered card is absent or disabled in registry: $($source.BaseName) ($($source.FullName))."
    }
}

$activeRelicCodeSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@($activeRelics.codeName),
    [System.StringComparer]::Ordinal)
$registeredRelicFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Scripts/Relics') `
    -Recurse -File -Filter '*.cs' | Where-Object {
        Select-String -LiteralPath $_.FullName -Pattern '\[RegisterRelic' -Quiet
    }
foreach ($source in $registeredRelicFiles) {
    if (-not $activeRelicCodeSet.Contains($source.BaseName)) {
        Add-ValidationWarning "Registered relic is absent or disabled in registry: $($source.BaseName) ($($source.FullName))."
    }
}

# Godot reports duplicate UIDs at import time; catch them without starting Godot.
$uidOwners = @{}
$importFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'SlayTheSpire2MGRMod') `
    -Recurse -File -Filter '*.import'
foreach ($import in $importFiles) {
    $uidLine = Select-String -LiteralPath $import.FullName -Pattern '^uid="([^"]+)"$' |
        Select-Object -First 1
    if ($null -eq $uidLine) { continue }

    $uid = $uidLine.Matches[0].Groups[1].Value
    if ($uidOwners.ContainsKey($uid)) {
        Add-ValidationError "Duplicate Godot UID '$uid': '$($uidOwners[$uid])' and '$($import.FullName)'."
    }
    else {
        $uidOwners[$uid] = $import.FullName
    }
}

$rewardCards = @($activeCards | Where-Object {
    $_.rarity -in @('Common', 'Uncommon', 'Rare') -and
    [int]($_.multiplayerOnly ?? 0) -ne 1
})
$distribution = $rewardCards | Group-Object rarity | ForEach-Object {
    [pscustomobject]@{ Rarity = $_.Name; Count = $_.Count }
}
$expectedCounts = @{ Common = 20; Uncommon = 35; Rare = 25 }
foreach ($rarity in $expectedCounts.Keys) {
    $actual = @($rewardCards | Where-Object rarity -eq $rarity).Count
    if ($actual -ne $expectedCounts[$rarity]) {
        Add-ValidationWarning "Reward pool $rarity count is $actual; expected baseline is $($expectedCounts[$rarity])."
    }
}

Write-Host "MGR content validation"
Write-Host "  Active cards:  $($activeCards.Count) (reward pool: $($rewardCards.Count))"
Write-Host "  Active relics: $($activeRelics.Count)"
Write-Host "  Import UIDs:   $($uidOwners.Count)"
foreach ($item in $distribution | Sort-Object Rarity) {
    Write-Host "  $($item.Rarity): $($item.Count)"
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "Warnings ($($warnings.Count)):" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  - $warning" -ForegroundColor Yellow
    }
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Errors ($($errors.Count)):" -ForegroundColor Red
    foreach ($errorMessage in $errors) {
        Write-Host "  - $errorMessage" -ForegroundColor Red
    }
}

if ($errors.Count -gt 0 -or ($WarningsAsErrors -and $warnings.Count -gt 0)) {
    exit 1
}

Write-Host ""
Write-Host "Validation passed." -ForegroundColor Green
