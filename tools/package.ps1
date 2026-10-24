<#
.SYNOPSIS
  Builds Release and assembles the Thunderstore package dist/MistWorms-<version>.zip (spec §17.1).
.DESCRIPTION
  Layout: manifest.json, README.md, CHANGELOG.md, icon.png,
          plugins/MistWorms/{MistWorms.dll, assets/**, Localization/**}.
  Validates the manifest against Plugin.ModVersion and tools/versions.json before zipping.
  The zip is written with forward-slash entry names (Compress-Archive on PowerShell 5.1 uses
  backslashes, which mod managers on Linux mis-extract).
#>
param(
    [switch]$SkipBuild,
    [switch]$SkipTests
)
. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem, System.Drawing

$tsDir = Join-Path $RepoRoot 'thunderstore'
$outDir = Join-Path $RepoRoot 'src\MistWorms\bin\Release\net472'
$errors = New-Object System.Collections.Generic.List[string]

# --- Manifest validation -------------------------------------------------------------------
$pluginCs = Get-Content (Join-Path $RepoRoot 'src\MistWorms\Plugin.cs') -Raw
$m = [regex]::Match($pluginCs, 'const\s+string\s+ModVersion\s*=\s*"([^"]+)"')
if (-not $m.Success) { throw 'Plugin.ModVersion not found in src/MistWorms/Plugin.cs' }
$modVersion = $m.Groups[1].Value

$manifestPath = Join-Path $tsDir 'manifest.json'
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

if ($manifest.name -ne 'MistWorms') { $errors.Add("manifest.name must be 'MistWorms' (got '$($manifest.name)')") }
if ($manifest.name -notmatch '^[A-Za-z0-9_]+$') { $errors.Add('manifest.name may only contain letters, digits and underscores') }
if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { $errors.Add("version_number '$($manifest.version_number)' is not Major.Minor.Patch") }
if ($manifest.version_number -ne $modVersion) { $errors.Add("version_number $($manifest.version_number) != Plugin.ModVersion $modVersion") }
if ($null -eq $manifest.website_url) { $errors.Add('website_url is missing (an empty string is allowed)') }
elseif (-not $manifest.website_url) { Write-Warning 'website_url is empty: set it to the GitHub repository before publishing.' }
if (-not $manifest.description) { $errors.Add('description is empty') }
elseif ($manifest.description.Length -gt 250) { $errors.Add("description is $($manifest.description.Length) characters (max 250)") }

$expectedDeps = @(
    "denikson-BepInExPack_Valheim-$($Versions.bepInExPack)",
    "ValheimModding-Jotunn-$($Versions.jotunn)"
)
$deps = @($manifest.dependencies)
foreach ($d in $expectedDeps) { if ($deps -notcontains $d) { $errors.Add("dependency '$d' missing (pinned in tools/versions.json)") } }
foreach ($d in $deps) { if ($expectedDeps -notcontains $d) { $errors.Add("unexpected dependency '$d'") } }

# Thunderstore rejects manifests with a UTF-8 BOM.
$bytes = [IO.File]::ReadAllBytes($manifestPath)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $errors.Add('manifest.json must be UTF-8 without BOM') }

foreach ($f in 'README.md', 'CHANGELOG.md') {
    if (-not (Test-Path (Join-Path $tsDir $f))) { $errors.Add("thunderstore/$f is missing") }
}
$changelog = Join-Path $tsDir 'CHANGELOG.md'
if ((Test-Path $changelog) -and -not (Select-String -Path $changelog -Pattern ([regex]::Escape($modVersion)) -Quiet)) {
    Write-Warning "CHANGELOG.md has no entry for $modVersion."
}

$icon = Join-Path $tsDir 'icon.png'
$hasIcon = Test-Path $icon
if (-not $hasIcon) {
    Write-Warning 'thunderstore/icon.png is missing (rendered by assets/blender/render_icon.py in phase 2); Thunderstore will refuse the upload.'
} else {
    $img = [System.Drawing.Image]::FromFile($icon)
    try { if ($img.Width -ne 256 -or $img.Height -ne 256) { $errors.Add("icon.png is $($img.Width)x$($img.Height), must be 256x256") } }
    finally { $img.Dispose() }
}

if ($errors.Count) {
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    throw "Package validation failed ($($errors.Count) problem(s))."
}

# --- Build ---------------------------------------------------------------------------------
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -NoDeploy -SkipTests:$SkipTests
}
$dll = Join-Path $outDir 'MistWorms.dll'
if (-not (Test-Path $dll)) { throw "Release build not found: $dll" }

# The Release build must not carry the dev harness; its namespace name would be in the metadata.
$dllText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
if ($dllText.Contains('MistWorms.Dev')) { throw "$dll contains the MistWorms.Dev namespace: not a clean Release build." }

# --- Assemble ------------------------------------------------------------------------------
$dist = Join-Path $RepoRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zipPath = Join-Path $dist "MistWorms-$modVersion.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

$entries = New-Object System.Collections.Generic.List[object]
function Add-Entry([string]$source, [string]$entryName) { $entries.Add([pscustomobject]@{ Source = $source; Name = $entryName }) }
function Add-Tree([string]$sourceDir, [string]$prefix, [string]$filter = '*') {
    if (-not (Test-Path $sourceDir)) { Write-Warning "$sourceDir not found, '$prefix' left empty."; return }
    $root = (Resolve-Path $sourceDir).Path.TrimEnd('\') + '\'
    Get-ChildItem $sourceDir -Recurse -File -Filter $filter | ForEach-Object {
        Add-Entry $_.FullName ($prefix + $_.FullName.Substring($root.Length).Replace('\', '/'))
    }
}

Add-Entry $manifestPath 'manifest.json'
Add-Entry (Join-Path $tsDir 'README.md') 'README.md'
Add-Entry $changelog 'CHANGELOG.md'
if ($hasIcon) { Add-Entry $icon 'icon.png' }
Add-Entry $dll 'plugins/MistWorms/MistWorms.dll'
# The folder also holds MWLocalization.cs; only the language files ship.
Add-Tree (Join-Path $RepoRoot 'src\MistWorms\Localization') 'plugins/MistWorms/Localization/' '*.json'
Add-Tree (Join-Path $RepoRoot 'assets\build') 'plugins/MistWorms/assets/'

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($e in $entries) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $e.Source, $e.Name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }

$entries | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Step "Package written: $zipPath ($([math]::Round((Get-Item $zipPath).Length / 1KB)) KB)"
Write-Host 'Validate it at https://thunderstore.io/tools/package-validation/ before publishing.'
