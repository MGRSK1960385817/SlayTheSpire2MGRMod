[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$V107DataDir,

    [Parameter(Mandatory)]
    [string]$V111DataDir,

    [string]$PckPath,

    [string]$OutputDir,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot '.artifacts\MGRMod-cross-version'
}
if ([string]::IsNullOrWhiteSpace($PckPath)) {
    $PckPath = Join-Path $repoRoot '..\MGRWorkshop\content\MGRMod.pck'
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDir)
$v107Dir = [System.IO.Path]::GetFullPath($V107DataDir)
$v111Dir = [System.IO.Path]::GetFullPath($V111DataDir)
$pckFullPath = [System.IO.Path]::GetFullPath($PckPath)
foreach ($dataDir in @($v107Dir, $v111Dir)) {
    if (-not (Test-Path -LiteralPath (Join-Path $dataDir 'sts2.dll'))) {
        throw "Missing sts2.dll in compatibility reference directory: $dataDir"
    }
}
if (-not (Test-Path -LiteralPath $pckFullPath)) {
    throw "Missing MGRMod.pck: $pckFullPath"
}

[void](New-Item -ItemType Directory -Path $outputRoot -Force)
[void](New-Item -ItemType Directory -Path (Join-Path $outputRoot 'lib\0.107.1') -Force)
[void](New-Item -ItemType Directory -Path (Join-Path $outputRoot 'lib\0.111.0') -Force)

function Build-Payload {
    param(
        [string]$CompatTarget,
        [string]$DataDir
    )

    & dotnet build (Join-Path $repoRoot 'MGRMod.csproj') `
        -c $Configuration `
        "/p:Sts2CompatTarget=$CompatTarget" `
        "/p:Sts2DataDir=$DataDir" `
        /p:BuildCrossVersionBundle=false `
        /p:CopyModOnBuild=false `
        /p:RunPckExport=false | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "MGR payload build failed for $CompatTarget."
    }

    $payload = Join-Path $repoRoot ".godot\mono\temp\bin\$Configuration\MGRMod.dll"
    if (-not (Test-Path -LiteralPath $payload)) {
        throw "Build succeeded but payload was not found: $payload"
    }
    $destination = Join-Path $outputRoot "lib\$CompatTarget\MGRMod.dll"
    Copy-Item -LiteralPath $payload -Destination $destination -Force
    return $destination
}

# Build newest first so a v0.111 reference directory may safely point at a
# preserved build cache without the v0.107 build overwriting it beforehand.
$payload111 = Build-Payload -CompatTarget '0.111.0' -DataDir $v111Dir
$payload107 = Build-Payload -CompatTarget '0.107.1' -DataDir $v107Dir

& dotnet build (Join-Path $repoRoot 'Loader\MGRMod.Loader.csproj') `
    -c $Configuration `
    "/p:Sts2DataDir=$v107Dir"
if ($LASTEXITCODE -ne 0) {
    throw 'MGR variant loader build failed.'
}

$loader = Join-Path $repoRoot "Loader\bin\$Configuration\net9.0\MGRMod.Loader.dll"
Copy-Item -LiteralPath $loader -Destination (Join-Path $outputRoot 'MGRMod.dll') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'MGRMod.json') -Destination $outputRoot -Force
$pckDestination = [System.IO.Path]::GetFullPath((Join-Path $outputRoot 'MGRMod.pck'))
if (-not $pckFullPath.Equals(
        $pckDestination,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $pckFullPath -Destination $pckDestination -Force
}

$hash107 = (Get-FileHash -LiteralPath $payload107 -Algorithm SHA256).Hash.ToLowerInvariant()
$hash111 = (Get-FileHash -LiteralPath $payload111 -Algorithm SHA256).Hash.ToLowerInvariant()
$variantManifest = [ordered]@{
    schema = 1
    variants = @(
        [ordered]@{
            compatTarget = '0.107.1'
            directory = 'lib/0.107.1'
            assembly = 'MGRMod.dll'
            sha256 = $hash107
        },
        [ordered]@{
            compatTarget = '0.111.0'
            directory = 'lib/0.111.0'
            assembly = 'MGRMod.dll'
            sha256 = $hash111
        }
    )
}
$manifestJson = $variantManifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText(
    (Join-Path $outputRoot 'mgrmod-variants.manifest'),
    $manifestJson + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

$loaderIdentity = [System.Reflection.AssemblyName]::GetAssemblyName(
    (Join-Path $outputRoot 'MGRMod.dll')).Name
$payload107Identity = [System.Reflection.AssemblyName]::GetAssemblyName($payload107).Name
$payload111Identity = [System.Reflection.AssemblyName]::GetAssemblyName($payload111).Name
if ($loaderIdentity -ne 'MGRMod.Loader' -or
    $payload107Identity -ne 'MGRMod' -or
    $payload111Identity -ne 'MGRMod') {
    throw "Unexpected assembly identities: loader=$loaderIdentity, v107=$payload107Identity, v111=$payload111Identity"
}

Write-Host "MGR cross-version bundle prepared: $outputRoot"
Write-Host "  Loader:  MGRMod.Loader"
Write-Host "  v0.107:  $hash107"
Write-Host "  v0.111:  $hash111"
