# Shared helpers dot-sourced by the other tools/*.ps1 scripts.
$ErrorActionPreference = 'Stop'

$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:Versions = Get-Content (Join-Path $PSScriptRoot 'versions.json') -Raw | ConvertFrom-Json
$script:DevDir = if ($env:MW_DEV_DIR) { $env:MW_DEV_DIR } else { 'C:\ValheimDev' }
$script:DevServerDir = if ($env:MW_DEV_SERVER_DIR) { $env:MW_DEV_SERVER_DIR } else { 'C:\ValheimDevServer' }
$script:SteamCmdDir = if ($env:MW_STEAMCMD_DIR) { $env:MW_STEAMCMD_DIR } else { 'C:\SteamCMD' }
$script:CacheDir = Join-Path $env:LOCALAPPDATA 'MistWorms\cache'

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# The x86 dotnet host can shadow the x64 one on PATH and has no SDK; always prefer the x64 SDK.
function Get-Dotnet {
    $x64 = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path $x64) { return $x64 }
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw 'dotnet SDK not found. Run tools/install-tools.ps1.'
}

function Get-SteamPath {
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
        $p = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($p.SteamPath) { return ($p.SteamPath -replace '/', '\') }
        if ($p.InstallPath) { return $p.InstallPath }
    }
    throw 'Steam not found in the registry.'
}

# Finds the Steam library that holds the given app id by parsing libraryfolders.vdf.
function Get-SteamAppDir([int]$appId, [string]$installDir) {
    $steam = Get-SteamPath
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    $libs = @($steam)
    if (Test-Path $vdf) {
        $libs += Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value -replace '\\\\', '\' }
    }
    foreach ($lib in ($libs | Select-Object -Unique)) {
        if (Test-Path (Join-Path $lib "steamapps\appmanifest_$appId.acf")) {
            $dir = Join-Path $lib "steamapps\common\$installDir"
            if (Test-Path $dir) { return $dir }
        }
    }
    throw "Steam app $appId ($installDir) is not installed."
}

function Get-ThunderstorePackage([string]$namespace, [string]$name, [string]$version) {
    New-Item -ItemType Directory -Force $script:CacheDir | Out-Null
    $zip = Join-Path $script:CacheDir "$namespace-$name-$version.zip"
    if (-not (Test-Path $zip)) {
        Write-Step "Downloading $namespace-$name $version"
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest "https://thunderstore.io/package/download/$namespace/$name/$version/" -OutFile $zip -UseBasicParsing
    }
    return $zip
}

function Expand-Fresh([string]$zip, [string]$dest) {
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    Expand-Archive -Path $zip -DestinationPath $dest
}

# Installs BepInExPack_Valheim and Jotunn into a game (client or dedicated server) directory.
function Install-ModStack([string]$gameDir) {
    $bepZip = Get-ThunderstorePackage 'denikson' 'BepInExPack_Valheim' $script:Versions.bepInExPack
    $jotZip = Get-ThunderstorePackage 'ValheimModding' 'Jotunn' $script:Versions.jotunn
    $tmp = Join-Path $script:CacheDir 'extract'

    Write-Step "Installing BepInExPack_Valheim $($script:Versions.bepInExPack) into $gameDir"
    Expand-Fresh $bepZip $tmp
    $pack = Join-Path $tmp 'BepInExPack_Valheim'
    if (-not (Test-Path $pack)) { throw "Unexpected BepInExPack layout in $bepZip" }
    Copy-Item -Path (Join-Path $pack '*') -Destination $gameDir -Recurse -Force

    Write-Step "Installing Jotunn $($script:Versions.jotunn)"
    Expand-Fresh $jotZip $tmp
    $jotDest = Join-Path $gameDir 'BepInEx\plugins\Jotunn'
    New-Item -ItemType Directory -Force $jotDest | Out-Null
    $src = if (Test-Path (Join-Path $tmp 'plugins')) { Join-Path $tmp 'plugins' } else { $tmp }
    Get-ChildItem $src -Recurse -File -Include *.dll, *.pdb, *.mdb, *.xml | Copy-Item -Destination $jotDest -Force
    Remove-Item -Recurse -Force $tmp
}
