[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release', [switch]$Package)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Join-Path $repo 'MechrevoBatteryManager'
$candidates = @((Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'), (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'))
$msbuild = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild not found.' }
$projects = @('MechrevoBatteryManager.Core\MechrevoBatteryManager.Core.csproj', 'MechrevoBatteryManager.Service\MechrevoBatteryManager.Service.csproj', 'MechrevoBatteryManager.Gui\MechrevoBatteryManager.Gui.csproj')
foreach ($project in $projects) { & $msbuild (Join-Path $projectRoot $project) /p:Configuration=$Configuration /p:Platform=x64 /verbosity:minimal; if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" } }
$dist = Join-Path $projectRoot 'dist'; New-Item -ItemType Directory -Path $dist -Force | Out-Null
Get-ChildItem -LiteralPath $dist -File | Remove-Item -Force
Copy-Item (Join-Path $projectRoot "MechrevoBatteryManager.Core\bin\$Configuration\MechrevoBatteryManager.Core.dll") (Join-Path $dist 'MechrevoBatteryManager.Core.dll') -Force
Copy-Item (Join-Path $projectRoot "MechrevoBatteryManager.Gui\bin\$Configuration\MechrevoBatteryManager.exe") (Join-Path $dist 'MechrevoBatteryManager.exe') -Force
Copy-Item (Join-Path $projectRoot "MechrevoBatteryManager.Service\bin\$Configuration\MechrevoBatteryManager.Service.exe") (Join-Path $dist 'MechrevoBatteryManager.Service.payload') -Force
Copy-Item (Join-Path $projectRoot 'MechrevoBatteryManager.Service\Uninstall-MechrevoBatteryManager.cmd') (Join-Path $dist 'Uninstall-MechrevoBatteryManager.cmd') -Force
Write-Host "Build complete: $dist"
if ($Package) {
  $release = Join-Path $repo 'release'; New-Item -ItemType Directory -Path $release -Force | Out-Null
  $archive = Join-Path $release ("MechrevoBatteryManager-v1.2.0-win-x64.zip")
  if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
  $staging = Join-Path $release 'package'; if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }; New-Item -ItemType Directory -Path $staging | Out-Null
  Copy-Item -LiteralPath $dist -Destination (Join-Path $staging 'dist') -Recurse
  Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $staging 'README.md')
  Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -Force
  Remove-Item -LiteralPath $staging -Recurse -Force
  Write-Host "Package created: $archive"
}
