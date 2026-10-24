<#
.SYNOPSIS
  Decompiles the game assemblies into reference/decompiled/ (gitignored), the source of truth for
  signatures, field names and prefab logic (spec §1.3). Requires tools/setup-game.ps1 first.
#>
param([string[]]$Assemblies = @('assembly_valheim', 'assembly_utils', 'assembly_guiutils'))
. (Join-Path $PSScriptRoot 'common.ps1')

$ilspy = Join-Path $env:USERPROFILE '.dotnet\tools\ilspycmd.exe'
if (-not (Test-Path $ilspy)) { $ilspy = (Get-Command ilspycmd -ErrorAction Stop).Source }
$refDir = Join-Path $RepoRoot 'reference\game-dlls'
$outRoot = Join-Path $RepoRoot 'reference\decompiled'

foreach ($name in $Assemblies) {
    $dll = Join-Path $refDir "$name.dll"
    if (-not (Test-Path $dll)) { Write-Warning "$dll not found, skipped"; continue }
    $out = Join-Path $outRoot $name
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    New-Item -ItemType Directory -Force $out | Out-Null
    Write-Step "ilspycmd $name"
    # One .cs file per type, with references resolved from the same folder.
    & $ilspy -p -o $out -r $refDir $dll
    if ($LASTEXITCODE -ne 0) { throw "ilspycmd failed for $name" }
}
Write-Step "Decompiled into $outRoot"
