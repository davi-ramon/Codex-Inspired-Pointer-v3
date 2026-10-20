$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Codex Pointer'
$localRoot = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\')
$resolved = [IO.Path]::GetFullPath($installDir).TrimEnd('\')
if (-not $resolved.StartsWith($localRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($resolved) -ne 'Codex Pointer') {
    throw 'Unexpected installation path; uninstall stopped.'
}
$backupFile = Join-Path $installDir 'previous-cursors.json'
if (-not (Test-Path -LiteralPath $backupFile)) { throw 'Original cursor backup is missing; no changes were made.' }

$stopFlag = Join-Path $installDir 'stop.flag'
Set-Content -LiteralPath $stopFlag -Value 'stop' -Encoding ASCII
Start-Sleep -Milliseconds 900
$backup = Get-Content -LiteralPath $backupFile -Raw | ConvertFrom-Json
$cursorKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Control Panel\Cursors')
foreach ($entry in $backup.Entries) {
    $name = [string]$entry.Name
    if ($entry.Exists) {
        $kind = [Enum]::Parse([Microsoft.Win32.RegistryValueKind], [string]$entry.Kind)
        $value = $entry.Value
        if ($kind -eq [Microsoft.Win32.RegistryValueKind]::DWord) { $value = [int]$value }
        $cursorKey.SetValue($name, $value, $kind)
    } else {
        $cursorKey.DeleteValue($name, $false)
    }
}
$cursorKey.Close()
$schemesKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Control Panel\Cursors\Schemes')
$schemesKey.DeleteValue('Codex-inspired Blue Glow 2', $false)
$schemesKey.Close()
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$runKey.DeleteValue('CodexInspiredPointerMotionV2', $false)
$runKey.DeleteValue('CodexInspiredPointerMotionV3', $false)
$runKey.Close()
Unregister-ScheduledTask -TaskName 'CodexInspiredPointerMotionV3' -Confirm:$false -ErrorAction SilentlyContinue

$native = @'
using System;
using System.Runtime.InteropServices;
public static class CursorRefreshV2 {
 [DllImport("user32.dll", SetLastError=true)] public static extern bool SystemParametersInfo(uint a,uint b,IntPtr c,uint d);
}
'@
Add-Type -TypeDefinition $native
if (-not [CursorRefreshV2]::SystemParametersInfo(0x57, 0, [IntPtr]::Zero, 0)) {
    Write-Warning 'The cursor scheme was restored in the registry. Sign out and back in to refresh it.'
}
try { Remove-Item -LiteralPath $installDir -Recurse -Force } catch {
    Write-Warning 'The old installed files could not all be removed, but Windows cursors were restored.'
}
Write-Host 'Previous Windows cursors restored.' -ForegroundColor Green
