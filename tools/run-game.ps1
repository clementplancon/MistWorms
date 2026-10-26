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
  tools/run-game.ps1 -Join 127.0.0.1:2456 -NoMod   # the client WITHOUT the mod (S17: the server must refuse it)
.NOTES
  -NoMod sets DOORSTOP_DISABLE=1 in the environment of the child process only: Unity Doorstop (winhttp.dll of
  the game folder, BepInEx 5 pack) reads that variable and does not load BepInEx, so the game starts vanilla.
  Verified in C:\ValheimDev: the name DOORSTOP_DISABLE is a string of winhttp.dll, and doorstop_config.ini has
  ignore_disable_switch = false (the switch is honoured). No file of the game folder is touched. Without the mod
  there is no dev harness (no autojoin, no bridge): the vanilla command line is used,
  -joinserverwithcharacter <host:port> <character> -password <pw> (FejdStartup.ParseArguments), so the
  character must exist. The process is left running: tools/vh.py s17 reads the server log, then kills it.
#>
param(
    [string]$World = 'MWTest',
    [string]$Character = 'MWTester',
    [int]$BridgePort = 27781,
    [string]$Join = '',
    # Only used with -Join; matches the tools/run-server.ps1 default.
    [string]$JoinPassword = 'mwdev123',
    # Start without BepInEx (vanilla client); needs -Join.
    [switch]$NoMod
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
if ($NoMod) {
    if (-not $Join) { throw '-NoMod needs -Join host:port (a vanilla client cannot autoload a world).' }
    # Vanilla arguments only: the dev harness (and -mwdev) is not loaded.
    $gameArgs = @('-console', '-window-mode', 'windowed', '-screen-width', '1280', '-screen-height', '720',
                  '-joinserverwithcharacter', $Join, $Character, '-password', $JoinPassword)
} elseif ($Join) {
    $gameArgs += "join=$Join"
    $gameArgs += "character=$Character"
    if ($JoinPassword) { $gameArgs += "password=$JoinPassword" }
} else {
    $gameArgs += "autoload=$World/$Character"
}
if (-not $NoMod) { $gameArgs += "bridge=$BridgePort" }

$already = Get-Process -Name 'valheim' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($DevDir, [StringComparison]::OrdinalIgnoreCase) }
if ($already) { Write-Warning "valheim.exe already running from $DevDir (PID $($already.Id -join ', ')); a second bridge needs a different -BridgePort." }

$prevRoot = $env:MW_REPO_ROOT
$prevDisable = $env:DOORSTOP_DISABLE
$env:MW_REPO_ROOT = $RepoRoot
if ($NoMod) { $env:DOORSTOP_DISABLE = '1' }
try {
    Write-Host "Launching $exe $($gameArgs -join ' ')$(if ($NoMod) { '   [DOORSTOP_DISABLE=1: no BepInEx, no mod]' })"
    $proc = Start-Process -FilePath $exe -ArgumentList $gameArgs -WorkingDirectory $DevDir -PassThru
} finally {
    $env:MW_REPO_ROOT = $prevRoot
    $env:DOORSTOP_DISABLE = $prevDisable
}
Write-Host "Log: $(Join-Path $DevDir 'BepInEx\LogOutput.log')"
Write-Output $proc.Id
