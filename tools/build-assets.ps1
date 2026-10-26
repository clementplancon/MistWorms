<#
.SYNOPSIS
  Regenerates every asset from the versioned Blender scripts (spec §13.1): headless Blender,
  no Unity, no hand-made binaries.
.DESCRIPTION
  Runs, in order: make_worm.py, make_knots.py, make_strands.py, make_nest.py, make_mass_preview.py (layout previews, helper of
  render_icon.py, never shipped), render_icon.py, validate_assets.py
  (common.py is a helper module imported by the others, never run directly). Missing scripts are
  skipped with a notice.
  Each Blender script is started as:
    blender --background --factory-startup --python-exit-code 1 --python <script> -- --repo-root <repo>
  validate_assets.py is plain Python (py -3). The build stops on a non-zero exit code or a Python
  traceback in the output.
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

$order = @('make_worm', 'make_knots', 'make_strands', 'make_nest', 'make_mass_preview', 'render_icon', 'validate_assets')
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

# Native stderr must not abort the script in PS 5.1 (ErrorActionPreference = Stop turns it into an exception).
function Invoke-Native([string]$exe, [string[]]$arguments) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $lines = & $exe @arguments 2>&1 | ForEach-Object { "$_" }
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    return [pscustomobject]@{ Code = $code; Lines = @($lines) }
}

# glTF exporter chatter and render progress are noise; everything else is shown.
$noise = '^\d\d:\d\d:\d\d \| INFO|^\s*$|^Fra:\d|^Time: \d|^Blender quit|^INFO Draco|^Blender \d'

$swAll = [Diagnostics.Stopwatch]::StartNew()
$timings = [ordered]@{}
foreach ($name in $order) {
    $script = Join-Path $scriptsDir "$name.py"
    if (-not (Test-Path $script)) { Write-Host "Notice: assets/blender/$name.py not found, skipped." -ForegroundColor Yellow; continue }
    Write-Step $name
    $sw = [Diagnostics.Stopwatch]::StartNew()
    if ($name -eq 'validate_assets') {
        # Pure Python, no bpy: prefer the system interpreter, fall back to Blender's bundled one.
        $py = Get-Command py -ErrorAction SilentlyContinue
        if ($py) { $r = Invoke-Native $py.Source @('-3', $script, '--repo-root', $RepoRoot) }
        else { $r = Invoke-Native $blenderExe @('--background', '--factory-startup', '--python-exit-code', '1', '--python', $script, '--', '--repo-root', $RepoRoot) }
    } else {
        $r = Invoke-Native $blenderExe @('--background', '--factory-startup', '--python-exit-code', '1', '--python', $script, '--', '--repo-root', $RepoRoot)
    }
    $r.Lines | Where-Object { $_ -notmatch $noise } | ForEach-Object { Write-Host $_ }
    $trace = $r.Lines | Where-Object { $_ -match '^Traceback \(most recent call last\)|^Error: Python:' } | Select-Object -First 1
    if ($r.Code -ne 0 -or $trace) { throw "$name.py failed (exit $($r.Code))$(if ($trace) { ' - Python traceback in output' })." }
    $timings[$name] = [math]::Round($sw.Elapsed.TotalSeconds, 1)
}

Write-Step 'Summary'
foreach ($k in $timings.Keys) { Write-Host ("  {0,-16} {1,6} s" -f $k, $timings[$k]) }
$build = Join-Path $RepoRoot 'assets\build'
$glb = @(Get-ChildItem $build -Filter *.glb -ErrorAction SilentlyContinue)
$png = @(Get-ChildItem $build -Filter *.png -ErrorAction SilentlyContinue)
Write-Host ("  {0} .glb ({1:N0} KB), {2} .png ({3:N0} KB), manifest.json {4}" -f $glb.Count, (($glb | Measure-Object Length -Sum).Sum / 1KB), $png.Count, (($png | Measure-Object Length -Sum).Sum / 1KB), $(if (Test-Path (Join-Path $build 'manifest.json')) { 'present' } else { 'MISSING' }))
Write-Host "  previews: $(Join-Path $build 'previews')"
Write-Step "Assets built in $([math]::Round($swAll.Elapsed.TotalSeconds, 1)) s"
