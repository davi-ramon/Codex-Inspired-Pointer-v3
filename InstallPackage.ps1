param(
    [ValidatePattern('^s(70|75|85|95|110|125|150)-b(130|150|170)$')]
    [string]$Preset = 's85-b150',
    [ValidateSet(0,1)][int]$EnableMotion = 1,
    [ValidateSet('Blue','Cyan','Purple','White')][string]$GlowColor = 'Blue',
    [ValidateSet(75,100,120,140)][int]$GlowRadius = 120,
    [ValidateSet(60,100,130,180)][int]$GlowIntensity = 130
)
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA 'Codex Pointer'
$assetsSource = Join-Path $source (Join-Path 'presets' (Join-Path $Preset 'assets'))
$assetsDir = Join-Path $installDir 'assets_active'
$backupFile = Join-Path $installDir 'previous-cursors.json'
$schemeName = 'Codex-inspired Blue Glow 2'
$roles = [ordered]@{
    Arrow='arrow.cur'; Help='help.cur'; AppStarting='working.ani'; Wait='busy.ani'
    Crosshair='crosshair.cur'; IBeam='ibeam.cur'; NWPen='pen.cur'; No='no.cur'
    SizeNS='sizens.cur'; SizeWE='sizewe.cur'; SizeNWSE='sizenwse.cur'
    SizeNESW='sizenesw.cur'; SizeAll='sizeall.cur'; UpArrow='uparrow.cur'
    Hand='hand.cur'; Pin='pin.cur'; Person='person.cur'
}

if (-not (Test-Path -LiteralPath (Join-Path $assetsSource 'arrow.cur'))) {
    throw "The $Preset preset is missing. Keep the complete package together."
}

New-Item -ItemType Directory -Force -Path $installDir, $assetsDir, (Join-Path $assetsDir 'motion') | Out-Null
$stopFlag = Join-Path $installDir 'stop.flag'
if (Test-Path -LiteralPath $backupFile) {
    Set-Content -LiteralPath $stopFlag -Value 'stop' -Encoding ASCII
    # Wait for the old helper and supervisor to release their mutexes before
    # copying assets. Otherwise the previous instance can continue replacing
    # cursors while the new scheme is being applied.
    $match = [regex]::Escape($installDir)
    for ($attempt = 0; $attempt -lt 32; $attempt++) {
        $running = @(Get-CimInstance Win32_Process -Filter "name='powershell.exe'" |
            Where-Object { $_.CommandLine -match $match -and
                $_.CommandLine -match 'CursorMotion(V3|Launcher)\.ps1' })
        if ($running.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    }
    if ($running.Count -gt 0) {
        throw 'The previous pointer helper is still stopping. Please run Apply to Windows again.'
    }
}

$cursorKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Control Panel\Cursors')
if (-not (Test-Path -LiteralPath $backupFile)) {
    $existing = $cursorKey.GetValueNames()
    $names = @('') + @($roles.Keys) + @('Scheme Source')
    $entries = foreach ($name in $names) {
        $exists = $existing -contains $name
        [pscustomobject]@{
            Name = $name; Exists = $exists
            Value = if ($exists) { $cursorKey.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } else { $null }
            Kind = if ($exists) { $cursorKey.GetValueKind($name).ToString() } else { $null }
        }
    }
    [pscustomobject]@{ Entries = @($entries) } | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath $backupFile -Encoding UTF8
}

Get-ChildItem -LiteralPath $assetsSource -File |
    Copy-Item -Destination $assetsDir -Force
Get-ChildItem -LiteralPath (Join-Path $assetsSource 'motion') -File |
    Copy-Item -Destination (Join-Path $assetsDir 'motion') -Force
if (-not (Test-Path -LiteralPath (Join-Path $assetsSource 'render'))) {
    throw "The $Preset render sources are missing. Keep the complete package together."
}
Copy-Item -LiteralPath (Join-Path $assetsSource 'render') -Destination $assetsDir -Recurse -Force
if ($GlowColor -ne 'Blue' -or $GlowRadius -ne 100 -or $GlowIntensity -ne 100) {
    $themeExe = Join-Path $source 'CursorTheme.exe'
    if (-not (Test-Path -LiteralPath $themeExe)) {
        throw 'CursorTheme.exe is missing from the package.'
    }
    & $themeExe $assetsDir $GlowColor $GlowRadius $GlowIntensity
    if ($LASTEXITCODE -ne 0) { throw 'Could not render the chosen glow settings.' }
}
Copy-Item -LiteralPath (Join-Path $source 'CursorMotionV3.cs') -Destination $installDir -Force
Copy-Item -LiteralPath (Join-Path $source 'CursorMotionV3.ps1') -Destination $installDir -Force
Copy-Item -LiteralPath (Join-Path $source 'CursorMotionLauncher.ps1') -Destination $installDir -Force
if (Test-Path -LiteralPath $stopFlag) { Remove-Item -LiteralPath $stopFlag -Force }

foreach ($role in $roles.Keys) {
    $cursorKey.SetValue($role, (Join-Path $assetsDir $roles[$role]), [Microsoft.Win32.RegistryValueKind]::String)
}
$cursorKey.SetValue('', $schemeName, [Microsoft.Win32.RegistryValueKind]::String)
$cursorKey.SetValue('Scheme Source', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
$scheme = (@($roles.Keys) | ForEach-Object { [string]$cursorKey.GetValue($_, '') }) -join ','
$cursorKey.Close()
$schemesKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Control Panel\Cursors\Schemes')
$schemesKey.SetValue($schemeName, $scheme, [Microsoft.Win32.RegistryValueKind]::ExpandString)
$schemesKey.Close()

$powershellExe = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path -LiteralPath $powershellExe)) {
    throw 'Windows PowerShell is required for the motion helper.'
}
$runner = Join-Path $installDir 'CursorMotionLauncher.ps1'
$runCommand = '"' + $powershellExe + '" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $runner + '"'
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$runKey.DeleteValue('CodexInspiredPointerMotionV2', $false)
if ($EnableMotion -eq 1) {
    $runKey.SetValue('CodexInspiredPointerMotionV3', $runCommand, [Microsoft.Win32.RegistryValueKind]::String)
} else {
    $runKey.DeleteValue('CodexInspiredPointerMotionV3', $false)
}
$runKey.Close()

# A delayed logon task is a second entry point if Explorer skips its Run startup
# value. The launcher mutex prevents duplicate supervisors.
$taskName = 'CodexInspiredPointerMotionV3'
try {
    if ($EnableMotion -eq 1) {
        $taskUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        $trigger = New-ScheduledTaskTrigger -AtLogOn -User $taskUser
        $trigger.Delay = 'PT20S'
        $invoke = '& ''' + $runner.Replace("'", "''") + ''''
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($invoke))
        $action = New-ScheduledTaskAction -Execute $powershellExe -Argument (
            '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand ' + $encoded)
        $principal = New-ScheduledTaskPrincipal -UserId $taskUser -LogonType Interactive -RunLevel Limited
        $taskSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds 0) `
            -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger `
            -Principal $principal -Settings $taskSettings -Force | Out-Null
    } else {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    }
} catch {
    Write-Warning "The delayed logon task could not be registered; the Run startup entry remains active: $_"
}

$native = @'
using System;
using System.Runtime.InteropServices;
public static class CursorRefreshV2 {
 [DllImport("user32.dll", SetLastError=true)] public static extern bool SystemParametersInfo(uint a,uint b,IntPtr c,uint d);
}
'@
Add-Type -TypeDefinition $native
if (-not [CursorRefreshV2]::SystemParametersInfo(0x57, 0, [IntPtr]::Zero, 0)) {
    throw 'Windows could not refresh the cursor scheme. Sign out and back in.'
}
Set-Content -LiteralPath (Join-Path $installDir 'settings.ini') -Value @(
    "Preset=$Preset", "Motion=$EnableMotion", "GlowColor=$GlowColor",
    "GlowRadius=$GlowRadius", "GlowIntensity=$GlowIntensity") -Encoding ASCII
if ($EnableMotion -eq 1) {
    Start-Process -FilePath $powershellExe -ArgumentList ('-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $runner + '"') -WindowStyle Hidden
}
Write-Host "Codex-inspired Pointer preset $Preset installed for this Windows account." -ForegroundColor Green
Write-Host 'Use Uninstall.cmd to restore the previous Windows cursors.'
