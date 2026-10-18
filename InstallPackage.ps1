param(
    [ValidatePattern('^s(70|75|85|95|110|125|150)-b(130|150|170)$')]
    [string]$Preset = 's85-b150',
    [ValidateSet(0,1)][int]$EnableMotion = 1
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
    Start-Sleep -Milliseconds 600
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
Copy-Item -LiteralPath (Join-Path $source 'CursorMotionV2.cs') -Destination $installDir -Force
Copy-Item -LiteralPath (Join-Path $source 'CursorMotionV2.ps1') -Destination $installDir -Force
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
$runner = Join-Path $installDir 'CursorMotionV2.ps1'
$runCommand = '"' + $powershellExe + '" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $runner + '"'
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
if ($EnableMotion -eq 1) {
    $runKey.SetValue('CodexInspiredPointerMotionV2', $runCommand, [Microsoft.Win32.RegistryValueKind]::String)
} else {
    $runKey.DeleteValue('CodexInspiredPointerMotionV2', $false)
}
$runKey.Close()

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
Set-Content -LiteralPath (Join-Path $installDir 'settings.ini') -Value @("Preset=$Preset", "Motion=$EnableMotion") -Encoding ASCII
if ($EnableMotion -eq 1) {
    Start-Process -FilePath $powershellExe -ArgumentList ('-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $runner + '"') -WindowStyle Hidden
}
Write-Host "Codex-inspired Pointer preset $Preset installed for this Windows account." -ForegroundColor Green
Write-Host 'Use Uninstall.cmd to restore the previous Windows cursors.'
