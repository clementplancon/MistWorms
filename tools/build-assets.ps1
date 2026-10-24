<#
.SYNOPSIS
  Regenerates every asset from the versioned Blender scripts (spec §13.1): headless Blender,
  no Unity, no hand-made binaries.
.DESCRIPTION
  Runs, in order: make_worm.py, make_mass.py, make_nest.py, render_icon.py, validate_assets.py
  (common.py is a helper module imported by the others, never run directly). Missing scripts are
  skipped with a notice until phase 2 writes them.
  Each script is started as:
    blender --background --factory-startup --python-exit-code 1 --python <script> -- --repo-root <repo>
  Outputs go to assets/build/ (validate_assets.py writes assets/build/manifest.json) and
  thunderstore/icon.png + banner.png. Blender ignores PYTHONPATH by default, so scripts must add
  their own folder to sys.path to import common.py.
.EXAMPLE
  tools/build-assets.ps1
  tools/build-assets.ps1 -Only make_worm, validate_assets
#>
param(
    [string[]]$Only,
    [string]$Blender
)
. (Join-Path $PSScriptRoot 'common.ps1')

$order = @('make_worm', 'make_mass', 'make_nest', 'render_icon', 'validate_assets')
if ($Only) {
    $unknown = $Only | Where-Object { $order -notcontains $_ }
    if ($unknown) { throw "Unknown script(s): $($unknown -join ', '). Known: $($order -join ', ')" }
    $order = $order | Where-Object { $Only -contains $_ }
}

function Find-Blender {
    if ($Blender) { if (Test-Path $Blender) { return $Blender } else { throw "Blender not found at $Blender" } }
    if ($env:MW_BLENDER -and (Test-Path $env:MW_BLENDER)) { return $env:MW_BLENDER }
    # Highest installed 4.x; Blender 5 changed Python APIs the scripts were not written against.
    $found = Get-ChildItem "$env:ProgramFiles\Blender Foundation\Blender 4.*\blender.exe" -ErrorAction SilentlyContinue |
        Sort-Object { [version]($_.Directory.Name -replace '^Blender\s+', '') } -Descending |
        Select-Object -First 1
    if ($found) { return $found.FullName }
    $cmd = Get-Command blender -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "Blender 4.x not found. Install it with tools/install-tools.ps1 (pinned $($Versions.blender)) or pass -Blender <path>."
}

$scriptsDir = Join-Path $RepoRoot 'assets\blender'
$present = $order | Where-Object { Test-Path (Join-Path $scriptsDir "$_.py") }
if (-not $present) {
    Write-Host "No Blender scripts in $scriptsDir yet (written in phase 2). Nothing to do."
    return
}

$blenderExe = Find-Blender
Write-Step "Blender: $blenderExe ($((Get-Item $blenderExe).VersionInfo.ProductVersion))"
New-Item -ItemType Directory -Force (Join-Path $RepoRoot 'assets\build') | Out-Null

$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($name in $order) {
    $script = Join-Path $scriptsDir "$name.py"
    if (-not (Test-Path $script)) { Write-Host "Notice: assets/blender/$name.py not found, skipped." -ForegroundColor Yellow; continue }
    Write-Step $name
    & $blenderExe --background --factory-startup --python-exit-code 1 --python $script -- --repo-root $RepoRoot
    if ($LASTEXITCODE -ne 0) { throw "$name.py failed (exit $LASTEXITCODE)." }
}
Write-Step "Assets built in $([math]::Round($sw.Elapsed.TotalSeconds, 1)) s"
