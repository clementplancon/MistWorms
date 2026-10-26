<#
.SYNOPSIS
  Builds the AssetBundle "mistworms_shaders" (WormRope and WormGPU shaders, materials, WormSim and MWTest compute shaders, variant collection) with the Unity editor in batchmode
  and copies it to assets/build/mistworms_shaders. tools/build.ps1 ships assets/build/** to <plugin>/assets, so the bundle lands next to the DLL
  (the mod loads <plugin>/assets/mistworms_shaders with AssetBundle.LoadFromFile).
.DESCRIPTION
  Needs the Unity editor of the game (override the path with -Unity or MW_UNITY). Run it only when assets/unity/MWShaders changes; the resulting
  bundle is committed with the other build assets. Checks the editor log for errors.
#>
param(
    [string]$Unity = $(if ($env:MW_UNITY) { $env:MW_UNITY } else { 'C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe' })
)
. (Join-Path $PSScriptRoot 'common.ps1')

if (-not (Test-Path $Unity)) { throw "Unity editor not found at $Unity (set MW_UNITY)." }
$project = Join-Path $RepoRoot 'assets\unity\MWShaders'
$log = Join-Path $project 'Logs\build-shaders.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
if (Test-Path $log) { Remove-Item $log }

# The textures of the bundle are the worm textures of assets/build (so the bundle never lags behind make_worm.py).
foreach ($t in 'worm_albedo', 'worm_gloss', 'worm_normal') {
    Copy-Item (Join-Path $RepoRoot "assets/build\$t.png") (Join-Path $project "Assets\Textures\$t.png") -Force
}

# The C# of the simulation host and its CPU reference are copied into the Editor folder of the project (ignored by git): SimCheck.cs runs the real
# compute shader and the real host in the editor (tools/check-shaders.ps1).
$gen = Join-Path $project 'Assets\Editor\Generated'
New-Item -ItemType Directory -Force $gen | Out-Null
foreach ($f in 'GpuWormTypes', 'WormSimRef', 'GpuWormTube', 'GpuWormHost', 'GpuSkinAnchors', 'GpuSkinSet', 'GpuSkinMetrics', 'GpuSolidMaps', 'GpuFreeWorms') {
    Copy-Item (Join-Path $RepoRoot "src\MistWorms\Render\Gpu\$f.cs") (Join-Path $gen "$f.cs") -Force
}

foreach ($a in 'GlbParser', 'JsonReader') { Copy-Item (Join-Path $RepoRoot "src\MistWorms\Assets\$a.cs") (Join-Path $gen "$a.cs") -Force }

Write-Step 'Unity batchmode: BuildBundles.Build'
$p = Start-Process -FilePath $Unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-quit', '-projectPath', "`"$project`"", '-buildTarget', 'StandaloneWindows64',
    '-executeMethod', 'BuildBundles.Build', '-logFile', "`"$log`"")
$text = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
$errors = @(Select-String -Path $log -Pattern 'error CS\d+|Shader error|BuildBundles: FAILED|BuildBundles: shader (Error|Warning)|Compilation failed|Scripts have compiler errors' -ErrorAction SilentlyContinue)
if ($p.ExitCode -ne 0 -or $errors.Count -gt 0 -or $text -notmatch 'BuildBundles: OK') {
    $errors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    throw "Unity build failed (exit $($p.ExitCode)); see $log"
}

$bundle = Join-Path $project 'Build\mistworms_shaders'
if (-not (Test-Path $bundle)) { throw "Bundle not produced: $bundle" }
$dest = Join-Path $RepoRoot 'assets\build'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item $bundle (Join-Path $dest 'mistworms_shaders') -Force
Select-String -Path $log -Pattern '^BuildBundles: ' | ForEach-Object { Write-Host $_.Line }
Write-Step "Bundle copied to $dest\mistworms_shaders ($((Get-Item $bundle).Length) bytes)"
