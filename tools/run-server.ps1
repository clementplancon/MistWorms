<#
.SYNOPSIS
  Starts or stops the local modded dedicated server in C:\ValheimDevServer (spec §15.1 step 3).
.DESCRIPTION
  Mirrors start_headless_server.bat (SteamAppId=892970, -nographics -batchmode) without -crossplay
  and with -public 0, so it is reachable only directly at 127.0.0.1:<Port>.
  The process runs without a console window; BepInEx writes BepInEx/LogOutput.log, raw stdout/stderr
  go to BepInEx/server-console.log and BepInEx/server-console.err.log. Prints the server PID.
  -Stop sends Ctrl+C first (the server saves the world on Ctrl+C) and kills it after a timeout.
  -ConfigFile <path> starts the server with that MistWorms .cfg (phase 8-2, S17: a synced admin-only key
  the client must see). BepInEx offers no clean per-process path for a plugin config, so the file of the
  server (BepInEx/config/dev.pointvirgule.mistworms.cfg) is saved aside as <file>.mwbackup, replaced, and
  restored by -Stop (or -RestoreConfig, or by the next start: a crash never leaves the test file in
  place for good). The other .cfg files of the server (SkyWhale, BepInEx) are never touched.
.EXAMPLE
  tools/run-server.ps1
  tools/run-server.ps1 -ConfigFile C:\Users\me\s17.cfg
  tools/run-server.ps1 -Stop
  tools/run-server.ps1 -RestoreConfig
#>
param(
    [string]$World = 'MWServer',
    [string]$Password = 'mwdev123',
    [int]$Port = 2456,
    [switch]$Stop,
    [int]$StopTimeoutSeconds = 30,
    [string]$ConfigFile = '',
    [switch]$RestoreConfig
)
. (Join-Path $PSScriptRoot 'common.ps1')

$serverName = 'MistWormsDev'
$exe = Join-Path $DevServerDir 'valheim_server.exe'
$bepDir = Join-Path $DevServerDir 'BepInEx'
$cfgPath = Join-Path $bepDir 'config\dev.pointvirgule.mistworms.cfg'
$cfgBackup = "$cfgPath.mwbackup"
$cfgNone = "$cfgPath.mwnone"      # marker: the server had no MistWorms .cfg before the test file was put there

# Puts the real MistWorms .cfg of the server back (idempotent: nothing to do when no test file is in place).
function Restore-MistWormsConfig {
    if (Test-Path $cfgBackup) {
        Move-Item -Force $cfgBackup $cfgPath
        Write-Step 'MistWorms .cfg of the server restored'
    } elseif (Test-Path $cfgNone) {
        Remove-Item -Force $cfgNone
        if (Test-Path $cfgPath) { Remove-Item -Force $cfgPath }
        Write-Step 'MistWorms test .cfg removed (the server had none)'
    }
}

function Get-ServerProcess {
    Get-Process -Name 'valheim_server' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($DevServerDir, [StringComparison]::OrdinalIgnoreCase) }
}

if ($RestoreConfig) {
    if (Get-ServerProcess) { throw 'The server is running: stop it first (tools/run-server.ps1 -Stop restores the .cfg by itself).' }
    Restore-MistWormsConfig
    return
}

if ($Stop) {
    $procs = @(Get-ServerProcess)
    if (-not $procs) { Write-Host 'Local server is not running.'; Restore-MistWormsConfig; return }
    foreach ($p in $procs) {
        Write-Step "Stopping valheim_server (PID $($p.Id))"
        # AttachConsole needs a process without its own console, so the Ctrl+C is sent from a helper.
        $helper = @"
Add-Type -Namespace W -Name K -MemberDefinition '
[DllImport("kernel32.dll")] public static extern bool FreeConsole();
[DllImport("kernel32.dll")] public static extern bool AttachConsole(uint pid);
[DllImport("kernel32.dll")] public static extern bool SetConsoleCtrlHandler(System.IntPtr h, bool add);
[DllImport("kernel32.dll")] public static extern bool GenerateConsoleCtrlEvent(uint ev, uint group);'
[W.K]::FreeConsole() | Out-Null
if (-not [W.K]::AttachConsole($($p.Id))) { exit 1 }
[W.K]::SetConsoleCtrlHandler([IntPtr]::Zero, `$true) | Out-Null
if (-not [W.K]::GenerateConsoleCtrlEvent(0, 0)) { exit 1 }
exit 0
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($helper))
        $h = Start-Process powershell.exe -ArgumentList '-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded -WindowStyle Hidden -PassThru -Wait
        if ($h.ExitCode -eq 0 -and $p.WaitForExit($StopTimeoutSeconds * 1000)) {
            Write-Step 'Server stopped cleanly (world saved).'
        } else {
            Write-Warning "Graceful stop failed or timed out after $StopTimeoutSeconds s; killing (unsaved progress is lost)."
            Stop-Process -Id $p.Id -Force
            Start-Sleep -Seconds 2
        }
    }
    Restore-MistWormsConfig
    return
}

if (-not (Test-Path $exe)) { throw "$exe not found. Run tools/setup-server.ps1 first." }
# Vanilla rules: password >= 5 characters and not contained in the server name.
if ($Password.Length -lt 5) { throw 'Password must be at least 5 characters.' }
if ($serverName.IndexOf($Password, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw 'Password must not appear in the server name.' }
if ($World -match '\s' -or $Password -match '\s') { throw 'World and password must not contain whitespace.' }

$running = Get-ServerProcess
if ($running) {
    Write-Warning "Local server already running (PID $($running.Id -join ', ')). Use -Stop first."
    Write-Output $running[0].Id
    return
}

if ($ConfigFile) {
    if (-not (Test-Path $ConfigFile)) { throw "-ConfigFile $ConfigFile not found." }
    Restore-MistWormsConfig     # a backup left by a crashed run holds the real file: put it back before saving it again
    if (Test-Path $cfgPath) { Copy-Item -Force $cfgPath $cfgBackup } else { New-Item -ItemType File -Force $cfgNone | Out-Null }
    Copy-Item -Force $ConfigFile $cfgPath
    Write-Step "MistWorms .cfg of the server replaced by $ConfigFile (the original is kept as $cfgBackup, restored by -Stop)"
} else {
    Restore-MistWormsConfig     # an ordinary start never runs on a leftover test file
}

$serverArgs = "-nographics -batchmode -name $serverName -port $Port -world $World -password $Password -public 0"
$outLog = Join-Path $bepDir 'server-console.log'
$errLog = Join-Path $bepDir 'server-console.err.log'

# cmd.exe in a fresh hidden console does the file redirection: the server outlives this shell,
# never shares its console (so -Stop's Ctrl+C cannot reach the caller) and needs no pipe reader.
$psi = New-Object System.Diagnostics.ProcessStartInfo 'cmd.exe'
$psi.Arguments = "/d /s /c `"`"$exe`" $serverArgs > `"$outLog`" 2> `"$errLog`"`""
$psi.WorkingDirectory = $DevServerDir
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.EnvironmentVariables['SteamAppId'] = "$($Versions.steamAppClient)"

Write-Step "Starting $exe $serverArgs"
$shell = [System.Diagnostics.Process]::Start($psi)

$proc = $null
$deadline = (Get-Date).AddSeconds(15)
while (-not $proc -and (Get-Date) -lt $deadline -and -not $shell.HasExited) {
    Start-Sleep -Milliseconds 250
    $proc = Get-ServerProcess | Select-Object -First 1
}
if (-not $proc) { throw "valheim_server.exe did not start. See $errLog and $outLog." }

Write-Host "Server '$serverName' world '$World' on 127.0.0.1:$Port (password $Password)."
Write-Host "Log: $(Join-Path $bepDir 'LogOutput.log')"
Write-Output $proc.Id
