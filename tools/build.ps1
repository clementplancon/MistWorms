<#
.SYNOPSIS
  Runs the out-of-game tests, builds the plugin and deploys it into the dev game copy and the
  local dedicated server (spec §1.4, §15.6).
.DESCRIPTION
  Order: dotnet test (fail fast) -> dotnet build -> deploy. Deployed layout, per plugin dir:
    MistWorms.dll (+ MistWorms.pdb in Debug), Localization/*.json, assets/** (from assets/build/).
  Release excludes src/MistWorms/Dev/** and the MW_DEV symbol (see MistWorms.csproj).
.EXAMPLE
  tools/build.ps1
  tools/build.ps1 -Configuration Release -NoDeploy
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipTests,
    [switch]$NoDeploy
)
. (Join-Path $PSScriptRoot 'common.ps1')

$dotnet = Get-Dotnet
$pluginProj = Join-Path $RepoRoot 'src\MistWorms\MistWorms.csproj'
$testProj = Join-Path $RepoRoot 'src\MistWorms.Tests\MistWorms.Tests.csproj'
$outDir = Join-Path $RepoRoot "src\MistWorms\bin\$Configuration\net472"
$assetsBuild = Join-Path $RepoRoot 'assets\build'
$locSrc = Join-Path $RepoRoot 'src\MistWorms\Localization'

if (-not (Test-Path (Join-Path $RepoRoot 'reference\game-dlls\assembly_valheim.dll'))) {
    throw 'reference/game-dlls is empty. Run tools/setup-game.ps1 first.'
}

if ($SkipTests) {
    Write-Warning 'Tests skipped (-SkipTests).'
} elseif (-not (Test-Path $testProj)) {
    Write-Warning "Test project not found at $testProj, tests skipped."
} else {
    Write-Step "dotnet test ($Configuration)"
    & $dotnet test $testProj -c $Configuration --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE). Nothing was deployed." }
}

Write-Step "dotnet build MistWorms ($Configuration)"
& $dotnet build $pluginProj -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

$dll = Join-Path $outDir 'MistWorms.dll'
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }
Write-Step "Built $dll"

if ($NoDeploy) { return }

# Copying over a loaded DLL either fails or leaves the running game on stale code; refuse instead.
function Assert-NotRunning([string]$gameDir, [string[]]$names) {
    $root = [IO.Path]::GetFullPath($gameDir).TrimEnd('\') + '\'
    $running = Get-Process -Name $names -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) }
    if ($running) {
        $list = ($running | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
        throw "Cannot deploy: $list is running from $gameDir. Quit it first (vh quit / tools/run-server.ps1 -Stop) and rerun the build."
    }
}

function Deploy-Plugin([string]$gameDir) {
    $dest = Join-Path $gameDir 'BepInEx\plugins\MistWorms'
    New-Item -ItemType Directory -Force $dest | Out-Null

    # Mirror owned content only; anything else in the folder (e.g. user notes) is left alone.
    foreach ($stale in 'MistWorms.dll', 'MistWorms.pdb', 'Localization', 'assets') {
        $p = Join-Path $dest $stale
        if (Test-Path $p) { Remove-Item -Recurse -Force $p }
    }

    Copy-Item $dll $dest
    if ($Configuration -eq 'Debug') {
        $pdb = Join-Path $outDir 'MistWorms.pdb'
        if (Test-Path $pdb) { Copy-Item $pdb $dest }
    }

    $locDest = Join-Path $dest 'Localization'
    New-Item -ItemType Directory -Force $locDest | Out-Null
    if (Test-Path $locSrc) { Copy-Item (Join-Path $locSrc '*.json') $locDest }
    else { Write-Warning "No localization folder at $locSrc" }

    if (Test-Path $assetsBuild) {
        Copy-Item $assetsBuild (Join-Path $dest 'assets') -Recurse
    } else {
        Write-Warning 'assets/build/ does not exist yet (phase 2: tools/build-assets.ps1); deployed without assets.'
    }
    Write-Step "Deployed $Configuration to $dest"
}

$targets = @(@{ Dir = $DevDir; Procs = @('valheim') })
if (Test-Path (Join-Path $DevServerDir 'BepInEx\plugins')) { $targets += @{ Dir = $DevServerDir; Procs = @('valheim_server') } }
else { Write-Host "Local server not found at $DevServerDir, skipped." }

if (-not (Test-Path (Join-Path $DevDir 'BepInEx\plugins'))) { throw "Dev game copy not found at $DevDir. Run tools/setup-game.ps1." }

# Check every target before touching any, so a refusal never leaves client and server out of sync.
foreach ($t in $targets) { Assert-NotRunning $t.Dir $t.Procs }
foreach ($t in $targets) { Deploy-Plugin $t.Dir }
