<#
.SYNOPSIS
  Starts or stops the local modded dedicated server in C:\ValheimDevServer (spec §15.1 step 3).
.DESCRIPTION
  Mirrors start_headless_server.bat (SteamAppId=892970, -nographics -batchmode) without -crossplay
  and with -public 0, so it is reachable only directly at 127.0.0.1:<Port>.
  The process runs without a console window; BepInEx writes BepInEx/LogOutput.log, raw stdout/stderr
  go to BepInEx/server-console.log and BepInEx/server-console.err.log. Prints the server PID.
  -Stop sends Ctrl+C first (the server saves the world on Ctrl+C) and kills it after a timeout.
.EXAMPLE
  tools/run-server.ps1
  tools/run-server.ps1 -Stop
#>
param(
    [string]$World = 'MWServer',
    [string]$Password = 'mwdev123',
    [int]$Port = 2456,
    [switch]$Stop,
    [int]$StopTimeoutSeconds = 30
)
. (Join-Path $PSScriptRoot 'common.ps1')

$serverName = 'MistWormsDev'
$exe = Join-Path $DevServerDir 'valheim_server.exe'
$bepDir = Join-Path $DevServerDir 'BepInEx'

function Get-ServerProcess {
    Get-Process -Name 'valheim_server' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($DevServerDir, [StringComparison]::OrdinalIgnoreCase) }
}

if ($Stop) {
    $procs = @(Get-ServerProcess)
    if (-not $procs) { Write-Host 'Local server is not running.'; return }
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
        }
    }
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
