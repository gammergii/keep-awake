$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
Push-Location $PSScriptRoot
try {
    & $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /out:KeepAwake.exe /win32manifest:KeepAwake.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll KeepAwake.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
} finally { Pop-Location }
