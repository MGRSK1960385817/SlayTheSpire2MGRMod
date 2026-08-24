[CmdletBinding()]
param(
    [string]$BundleDir,

    [Parameter(Mandatory)]
    [string]$OutputZip
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($BundleDir)) {
    $BundleDir = Join-Path $repoRoot '.artifacts\MGRMod-cross-version'
}

$bundleRoot = [System.IO.Path]::GetFullPath($BundleDir)
$zipPath = [System.IO.Path]::GetFullPath($OutputZip)
$zipParent = Split-Path -Parent $zipPath
$zipLeaf = Split-Path -Leaf $zipPath
$expectedFiles = @(
    'MGRMod.dll',
    'MGRMod.json',
    'MGRMod.pck',
    'mgrmod-variants.manifest',
    'lib/0.107.1/MGRMod.dll',
    'lib/0.111.0/MGRMod.dll'
)

if (-not (Test-Path -LiteralPath $bundleRoot -PathType Container)) {
    throw "Bundle directory does not exist: $bundleRoot"
}

$actualFiles = @(
    Get-ChildItem -LiteralPath $bundleRoot -Recurse -File |
        ForEach-Object {
            [System.IO.Path]::GetRelativePath(
                $bundleRoot,
                $_.FullName).Replace('\', '/')
        }
)
$missing = @($expectedFiles | Where-Object { $actualFiles -cnotcontains $_ })
$unexpected = @($actualFiles | Where-Object { $expectedFiles -cnotcontains $_ })
if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
    throw "Bundle casing/structure mismatch. Missing: $($missing -join ', '); extra: $($unexpected -join ', ')."
}

[void](New-Item -ItemType Directory -Path $zipParent -Force)
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    "MGRMod-package-$([Guid]::NewGuid().ToString('N'))")
$stagingMod = Join-Path $temporaryRoot 'MGRMod'
$temporaryZip = Join-Path $zipParent (
    ".MGRMod-$([Guid]::NewGuid().ToString('N')).zip")
$backupZip = $null

try {
    [void](New-Item -ItemType Directory -Path $stagingMod -Force)
    foreach ($relativeName in $expectedFiles) {
        $source = Join-Path $bundleRoot $relativeName
        $destination = Join-Path $stagingMod $relativeName
        [void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
        Copy-Item -LiteralPath $source -Destination $destination
    }

    Compress-Archive -LiteralPath $stagingMod -DestinationPath $temporaryZip -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($temporaryZip)
    try {
        $archiveFiles = @(
            $archive.Entries |
                Where-Object { -not [string]::IsNullOrEmpty($_.Name) } |
                ForEach-Object { $_.FullName.Replace('\', '/') }
        )
    }
    finally {
        $archive.Dispose()
    }
    $expectedArchiveFiles = @($expectedFiles | ForEach-Object { "MGRMod/$_" })
    $archiveMissing = @(
        $expectedArchiveFiles |
            Where-Object { $archiveFiles -cnotcontains $_ }
    )
    $archiveUnexpected = @(
        $archiveFiles |
            Where-Object { $expectedArchiveFiles -cnotcontains $_ }
    )
    if ($archiveMissing.Count -gt 0 -or $archiveUnexpected.Count -gt 0) {
        throw "ZIP casing/structure mismatch. Missing: $($archiveMissing -join ', '); extra: $($archiveUnexpected -join ', ')."
    }

    $existing = @(
        Get-ChildItem -LiteralPath $zipParent -Force -File |
            Where-Object {
                $_.Name.Equals($zipLeaf, [System.StringComparison]::OrdinalIgnoreCase)
            }
    )
    if ($existing.Count -gt 1) {
        throw "Multiple case-insensitive ZIP matches for $zipLeaf in $zipParent."
    }
    if ($existing.Count -eq 1) {
        $backupZip = Join-Path $zipParent (
            ".MGRMod-backup-$([Guid]::NewGuid().ToString('N')).zip")
        Move-Item -LiteralPath $existing[0].FullName -Destination $backupZip
    }

    Move-Item -LiteralPath $temporaryZip -Destination $zipPath
    if ($null -ne $backupZip) {
        Remove-Item -LiteralPath $backupZip -Force
        $backupZip = $null
    }
}
catch {
    if ($null -ne $backupZip -and
        (Test-Path -LiteralPath $backupZip) -and
        -not (Test-Path -LiteralPath $zipPath)) {
        Move-Item -LiteralPath $backupZip -Destination $zipPath
        $backupZip = $null
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $temporaryZip) {
        Remove-Item -LiteralPath $temporaryZip -Force
    }
    $resolvedTemp = [System.IO.Path]::GetFullPath($temporaryRoot)
    $systemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($resolvedTemp.StartsWith($systemTemp, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemp).StartsWith('MGRMod-package-', [System.StringComparison]::Ordinal)) {
        if (Test-Path -LiteralPath $resolvedTemp) {
            Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
        }
    }
    else {
        throw "Refusing to clean unexpected temporary path: $resolvedTemp"
    }
}

Write-Host "MGR local ZIP prepared: $zipPath"
