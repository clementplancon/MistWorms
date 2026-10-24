<#
.SYNOPSIS
  Launches the modded dev copy of Valheim with the MistWorms dev harness (spec §15.2).
.DESCRIPTION
  Writes only the game PID to stdout (everything else goes to the host stream), so callers such
  as tools/vh.py can capture it. MW_REPO_ROOT is set for the child process only; the Debug build
  uses it to locate the repository (docs/, screenshots).
  Requires Steam to be running: the copy is started outside Steam and relies on steam_appid.txt.
.EXAMPLE
  tools/run-game.ps1                          # autoload MWTest/MWTester, bridge on 27781
  tools/run-game.ps1 -Join 127.0.0.1:2456     # join the local dedicated server instead
#>
param(
    [string]$World = 'MWTest',
    [string]$Character = 'MWTester',
    [int]$BridgePort = 27781,
    [string]$Join = '',
    # Only used with -Join; matches the tools/run-server.ps1 default.
    [string]$JoinPassword = 'mwdev123'
)
. (Join-Path $PSScriptRoot 'common.ps1')

$exe = Join-Path $DevDir 'valheim.exe'
if (-not (Test-Path $exe)) { throw "$exe not found. Run tools/setup-game.ps1 first." }

if (-not (Get-Process -Name 'steam' -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('ERROR: Steam is not running. Start Steam and log in, then rerun tools/run-game.ps1. Nothing was launched.')
    exit 2
}

# Start-Process joins arguments with spaces and does not quote them.
foreach ($v in $World, $Character, $Join, $JoinPassword) {
    if ($v -match '\s') { throw "Arguments must not contain whitespace: '$v'" }
}

$gameArgs = @('-console', '-window-mode', 'windowed', '-screen-width', '1280', '-screen-height', '720', '-mwdev')
if ($Join) {
    $gameArgs += "join=$Join"
    $gameArgs += "character=$Character"
    if ($JoinPassword) { $gameArgs += "password=$JoinPassword" }
} else {
    $gameArgs += "autoload=$World/$Character"
}
$gameArgs += "bridge=$BridgePort"

$already = Get-Process -Name 'valheim' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($DevDir, [StringComparison]::OrdinalIgnoreCase) }
if ($already) { Write-Warning "valheim.exe already running from $DevDir (PID $($already.Id -join ', ')); a second bridge needs a different -BridgePort." }

$prevRoot = $env:MW_REPO_ROOT
$env:MW_REPO_ROOT = $RepoRoot
try {
    Write-Host "Launching $exe $($gameArgs -join ' ')"
    $proc = Start-Process -FilePath $exe -ArgumentList $gameArgs -WorkingDirectory $DevDir -PassThru
} finally {
    $env:MW_REPO_ROOT = $prevRoot
}
Write-Host "Log: $(Join-Path $DevDir 'BepInEx\LogOutput.log')"
Write-Output $proc.Id
