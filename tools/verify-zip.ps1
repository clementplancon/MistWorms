<#
.SYNOPSIS
  Lists a Thunderstore package and fails if it carries a dev file or lacks a required one.
.DESCRIPTION
  Fails when the zip holds: a path with a Dev folder, previews, a golden file, a .pdb, or a MistWorms.dll that contains the dev harness
  (strings DevBridge, ScenarioRunner, MistWorms.Dev: a Profile or Debug build). Also fails when a required file is missing:
  manifest.json, README.md, icon.png, CHANGELOG.md, plugins/MistWorms/MistWorms.dll, the asset manifest, the shader bundle (GPU path),
  at least one .glb and the localization files. Entry names must use forward slashes.
  Default zip: dist/MistWorms-<manifest version>.zip.
#>
param([string]$Zip)
. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem, System.Drawing

if (-not $Zip) {
    $v = (Get-Content (Join-Path $RepoRoot 'thunderstore\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version_number
    $Zip = Join-Path $RepoRoot "dist\MistWorms-$v.zip"
}
if (-not (Test-Path $Zip)) { throw "Zip not found: $Zip" }

$errors = New-Object System.Collections.Generic.List[string]
$archive = [System.IO.Compression.ZipFile]::OpenRead($Zip)
try {
    $names = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName })
    Write-Step "Contents of $Zip"
    foreach ($e in $archive.Entries) { if ($e.Name) { Write-Host ("  {0,10:N0}  {1}" -f $e.Length, $e.FullName) } }

    foreach ($n in $names) {
        if ($n.Contains('\')) { $errors.Add("backslash in entry name: $n") }
        if ($n -match '(^|/)Dev(/|$)') { $errors.Add("Dev folder: $n") }
        if ($n -match '(?i)preview') { $errors.Add("preview file: $n") }
        if ($n -match '(?i)golden') { $errors.Add("golden file: $n") }
        if ($n -match '(?i)\.pdb$') { $errors.Add("pdb file: $n") }
    }

    $required = @('manifest.json', 'README.md', 'icon.png', 'CHANGELOG.md', 'plugins/MistWorms/MistWorms.dll',
        'plugins/MistWorms/assets/manifest.json', 'plugins/MistWorms/assets/mistworms_shaders',
        'plugins/MistWorms/Localization/en.json', 'plugins/MistWorms/Localization/fr.json')
    foreach ($r in $required) { if ($names -notcontains $r) { $errors.Add("required file missing: $r") } }
    if (-not ($names | Where-Object { $_ -like 'plugins/MistWorms/assets/*.glb' })) { $errors.Add('no .glb in plugins/MistWorms/assets') }

    $dllEntry = $archive.GetEntry('plugins/MistWorms/MistWorms.dll')
    if ($dllEntry) {
        $ms = New-Object IO.MemoryStream
        $s = $dllEntry.Open(); try { $s.CopyTo($ms) } finally { $s.Dispose() }
        $bytes = $ms.ToArray()
        # Metadata names are UTF-8, string literals UTF-16: look for both encodings.
        $text = [Text.Encoding]::ASCII.GetString($bytes)
        $text16 = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($str in 'DevBridge', 'ScenarioRunner', 'MistWorms.Dev') {
            if ($text.Contains($str) -or $text16.Contains($str)) { $errors.Add("MistWorms.dll contains '$str' (a Profile or Debug build, or dev code outside MW_DEV)") }
        }
    }

    $iconEntry = $archive.GetEntry('icon.png')
    if ($iconEntry) {
        $s = $iconEntry.Open()
        try {
            $img = [System.Drawing.Image]::FromStream($s)
            try { if ($img.Width -ne 256 -or $img.Height -ne 256) { $errors.Add("icon.png is $($img.Width)x$($img.Height), must be 256x256") } }
            finally { $img.Dispose() }
        } finally { $s.Dispose() }
    }
} finally { $archive.Dispose() }

if ($errors.Count) {
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    throw "verify-zip failed ($($errors.Count) problem(s))."
}
Write-Step "verify-zip OK ($($names.Count) files)"
