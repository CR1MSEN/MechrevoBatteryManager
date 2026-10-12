$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$core = Join-Path $repo 'MechrevoBatteryManager\dist\MechrevoBatteryManager.Core.dll'
if (-not (Test-Path -LiteralPath $core)) { throw 'Build Release first.' }
$out = Join-Path $env:TEMP ('mbm-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $out | Out-Null
try {
    Copy-Item -LiteralPath $core -Destination $out
    $exe = Join-Path $out 'MechrevoBatteryManager.Tests.exe'
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $csc /nologo /target:exe /platform:x64 "/out:$exe" "/reference:$core" (Join-Path $PSScriptRoot 'CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $gui = Join-Path $out 'GuiSmoke.exe'
    & $csc /nologo /target:exe /platform:x64 "/out:$gui" "/reference:$core" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll (Join-Path $PSScriptRoot 'GuiSmoke.cs') (Join-Path $repo 'MechrevoBatteryManager\MechrevoBatteryManager.Gui\MainForm.cs')
    if ($LASTEXITCODE -ne 0) { throw 'GUI test compilation failed.' }
    & $gui (Join-Path $out 'Chinese.png') (Join-Path $out 'English.png')
    if ($LASTEXITCODE -ne 0) { throw 'GUI tests failed.' }
} finally { Remove-Item -LiteralPath $out -Recurse -Force }
