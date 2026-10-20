$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CursorMotionV3.cs') -Raw
Add-Type -TypeDefinition $source -ReferencedAssemblies @('System.Windows.Forms', 'System.Drawing')
[CodexInspiredPointer.MotionV3]::Run($PSScriptRoot)
