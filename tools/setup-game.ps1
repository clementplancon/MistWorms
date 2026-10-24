<#
.SYNOPSIS
  Creates (or refreshes) the modded dev copy of Valheim in C:\ValheimDev (spec §1.2, §15.1).
  The Steam install is never modified. Re-run after a Steam update: robocopy /MIR keeps the
  copy in sync while preserving BepInEx and its doorstop files.
#>
param(
    [switch]$SkipCopy
)
. (Join-Path $PSScriptRoot 'common.ps1')

$steamGame = Get-SteamAppDir $Versions.steamAppClient 'Valheim'
Write-Step "Steam Valheim: $steamGame"

if (-not $SkipCopy) {
    Write-Step "Mirroring game into $DevDir (robocopy /MIR)"
    $keep = @('BepInEx', 'doorstop_libs')
    $keepFiles = @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt', 'start_game_bepinex.sh', 'start_server_bepinex.sh', 'steam_appid.txt')
    & robocopy $steamGame $DevDir /MIR /R:2 /W:2 /NFL /NDL /NP /NJH /XD @keep /XF @keepFiles
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
    # Running the copy outside Steam needs the app id next to the exe.
    Set-Content -Path (Join-Path $DevDir 'steam_appid.txt') -Value $Versions.steamAppClient -Encoding ascii -NoNewline
}

Install-ModStack $DevDir
New-Item -ItemType Directory -Force (Join-Path $DevDir 'BepInEx\plugins\MistWorms') | Out-Null

# Reference assemblies for compiling and decompiling (gitignored).
$refDir = Join-Path $RepoRoot 'reference\game-dlls'
Write-Step "Copying reference assemblies into $refDir"
New-Item -ItemType Directory -Force $refDir | Out-Null
Copy-Item (Join-Path $DevDir 'valheim_Data\Managed\*.dll') $refDir -Force
Copy-Item (Join-Path $DevDir 'BepInEx\core\*.dll') $refDir -Force
Copy-Item (Join-Path $DevDir 'BepInEx\plugins\Jotunn\Jotunn.dll') $refDir -Force

$unity = (Get-Item (Join-Path $DevDir 'UnityPlayer.dll')).VersionInfo.FileVersion
Write-Step "Done. Unity player $unity. Launch with tools/run-game.ps1"
exit 0 # robocopy leaves a non-zero $LASTEXITCODE on success
