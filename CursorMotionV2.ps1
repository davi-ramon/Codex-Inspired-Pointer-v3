$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CursorMotionV2.cs') -Raw
Add-Type -TypeDefinition $source -ReferencedAssemblies @('System.Windows.Forms', 'System.Drawing')
[CodexInspiredPointer.Motion]::Run($PSScriptRoot)
