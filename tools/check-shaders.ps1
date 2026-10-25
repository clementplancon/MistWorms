<#
.SYNOPSIS
  Runs SimCheck (assets/unity/MWShaders/Assets/Editor/SimCheck.cs) in the Unity editor, on the GPU of the machine: GPU against CPU parity of the worm rope simulation,
  a mass at rest, the engulfing of a capsule and pictures of the three LODs (assets/unity/MWShaders/Logs/simcheck_*.png). Run tools/build-shaders.ps1 first.
.DESCRIPTION
  Batchmode WITHOUT -nographics (the compute shader needs the D3D11 device). Never touches the game: only the Unity project of the shaders.
#>
param(
    [string]$Unity = $(if ($env:MW_UNITY) { $env:MW_UNITY } else { 'C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe' })
)
. (Join-Path $PSScriptRoot 'common.ps1')

if (-not (Test-Path $Unity)) { throw "Unity editor not found at $Unity (set MW_UNITY)." }
$project = Join-Path $RepoRoot 'assets\unity\MWShaders'
$log = Join-Path $project 'Logs\simcheck.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
if (Test-Path $log) { Remove-Item $log }

Write-Step 'Unity batchmode: SimCheck.Run'
$p = Start-Process -FilePath $Unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-quit', '-projectPath', "`"$project`"", '-executeMethod', 'SimCheck.Run', '-logFile', "`"$log`"")
Select-String -Path $log -Pattern '^SimCheck: ' | ForEach-Object { Write-Host $_.Line }
$errors = @(Select-String -Path $log -Pattern 'error CS\d+|Shader error|SimCheck: FAILED|SimCheck: FAIL ' -ErrorAction SilentlyContinue)
if ($p.ExitCode -ne 0 -or $errors.Count -gt 0) {
    $errors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    throw "SimCheck failed (exit $($p.ExitCode)); see $log"
}
Write-Step 'SimCheck OK'
