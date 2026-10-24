<#
.SYNOPSIS
  Installs a local Valheim dedicated server (SteamCMD app 896660) in C:\ValheimDevServer with
  BepInEx + Jotunn (spec §15.1 step 3). Run tools/install-tools.ps1 first for SteamCMD.
#>
. (Join-Path $PSScriptRoot 'common.ps1')

$steamcmd = Join-Path $SteamCmdDir 'steamcmd.exe'
if (-not (Test-Path $steamcmd)) { throw "SteamCMD missing at $steamcmd. Run tools/install-tools.ps1." }

Write-Step "SteamCMD app_update $($Versions.steamAppServer) into $DevServerDir"
New-Item -ItemType Directory -Force $DevServerDir | Out-Null
# The first SteamCMD run only self-updates and ignores the remaining commands: bootstrap it alone first.
& $steamcmd +quit | Out-Null
& $steamcmd +force_install_dir $DevServerDir +login anonymous +app_update $Versions.steamAppServer validate +quit
# SteamCMD exits with 7 after a successful first-run self-update; trust the presence of the exe instead.
if (-not (Test-Path (Join-Path $DevServerDir 'valheim_server.exe'))) { throw 'valheim_server.exe not found after SteamCMD.' }

Install-ModStack $DevServerDir
New-Item -ItemType Directory -Force (Join-Path $DevServerDir 'BepInEx\plugins\MistWorms') | Out-Null
Write-Step 'Dedicated server ready. Start it with tools/run-server.ps1'
exit 0
