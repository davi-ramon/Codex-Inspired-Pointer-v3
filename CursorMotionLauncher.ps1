$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$log = Join-Path $root 'motion-launcher.log'
$helper = Join-Path $root 'CursorMotionV3.ps1'
$powershellExe = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
try { Add-Content -LiteralPath $log -Value ("{0:s} launcher invoked pid={1}" -f (Get-Date), $PID) }
catch { }
$created = $false
$guard = [System.Threading.Mutex]::new($true, 'Local\CodexInspiredPointerLauncherV3', [ref]$created)
if (-not $created) {
    try { Add-Content -LiteralPath $log -Value ("{0:s} already running" -f (Get-Date)) } catch { }
    $guard.Dispose(); exit 0
}

# The logon session can expose cursor APIs before Explorer finishes applying the scheme.
# Keep one supervisor running so transient startup failures do not silently disable motion.
try { while ($true) {
    if (Test-Path -LiteralPath (Join-Path $root 'stop.flag')) { break }
    $settings = Join-Path $root 'settings.ini'
    if (-not (Test-Path -LiteralPath $settings) -or
        -not (Select-String -LiteralPath $settings -Pattern '^Motion=1$' -Quiet)) { break }
    try {
        $started = Get-Date
        $process = Start-Process -FilePath $powershellExe -ArgumentList @(
            '-NoProfile', '-WindowStyle', 'Hidden', '-ExecutionPolicy', 'Bypass',
            '-File', ('"' + $helper + '"')) -WindowStyle Hidden -PassThru -Wait
        $elapsed = [int]((Get-Date) - $started).TotalSeconds
        Add-Content -LiteralPath $log -Value ("{0:s} helper exited code={1} seconds={2}" -f
            (Get-Date), $process.ExitCode, $elapsed)
    } catch {
        Add-Content -LiteralPath $log -Value ("{0:s} launcher error: {1}" -f (Get-Date), $_)
    }
    if (Test-Path -LiteralPath (Join-Path $root 'stop.flag')) { break }
    Start-Sleep -Seconds 5
} } finally {
    try { $guard.ReleaseMutex() } catch { }
    $guard.Dispose()
}
