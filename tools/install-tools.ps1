<#
.SYNOPSIS
  Installs the developer tools listed in spec §1.3. Skips anything already present.
  Python >= 3.11 is accepted (spec asks for 3.12; tools/vh.py only needs the stdlib).
#>
param([switch]$Force)
. (Join-Path $PSScriptRoot 'common.ps1')

function Install-Winget([string]$id, [string]$version) {
    $wgArgs = @('install', '--id', $id, '-e', '--silent', '--accept-package-agreements', '--accept-source-agreements')
    if ($version) { $wgArgs += @('--version', $version) }
    Write-Step "winget $id $version"
    & winget @wgArgs
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1978335189) { Write-Warning "winget $id exited with $LASTEXITCODE" } # -1978335189 = already installed
}

if ($Force -or -not (Get-Command git -ErrorAction SilentlyContinue)) { Install-Winget 'Git.Git' }

$sdks = & (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe') --list-sdks 2>$null
if ($Force -or -not ($sdks -match '^8\.')) { Install-Winget 'Microsoft.DotNet.SDK.8' }

$blender = Get-ChildItem "$env:ProgramFiles\Blender Foundation\Blender 4.*\blender.exe" -ErrorAction SilentlyContinue
if ($Force -or -not $blender) { Install-Winget 'BlenderFoundation.Blender' $Versions.blender }

$pyOk = $false
try { $pyOk = [version](& py -3 -c "import sys;print('%d.%d' % sys.version_info[:2])") -ge [version]'3.11' } catch {}
if ($Force -or -not $pyOk) { Install-Winget 'Python.Python.3.12' }

if ($Force -or -not (Test-Path "$env:ProgramFiles\7-Zip\7z.exe")) { Install-Winget '7zip.7zip' }

$dotnet = Get-Dotnet
$tools = & $dotnet tool list -g
if ($Force -or -not ($tools -match 'ilspycmd')) {
    # ilspycmd 10.x targets .NET 10 and fails to install on the .NET 8/9 SDKs.
    Write-Step "dotnet tool install -g ilspycmd $($Versions.ilspycmd)"
    & $dotnet tool install -g ilspycmd --version $Versions.ilspycmd
}

if ($Force -or -not (Test-Path (Join-Path $SteamCmdDir 'steamcmd.exe'))) {
    Write-Step "Installing SteamCMD into $SteamCmdDir"
    New-Item -ItemType Directory -Force $SteamCmdDir | Out-Null
    $zip = Join-Path $CacheDir 'steamcmd.zip'
    New-Item -ItemType Directory -Force $CacheDir | Out-Null
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip' -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath $SteamCmdDir -Force
}

Write-Step 'Tools ready.'
