# Builds EAHideConnectorLabels.dll with the .NET Framework 4 compiler that ships with Windows.
# Works while EA is running: a DLL EA has loaded is renamed out of the way, and the new
# build takes effect the next time EA starts.
$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$eaDir   = 'C:\Program Files (x86)\Sparx Systems\EA'
$csc     = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$out     = Join-Path $root 'bin'
$dll     = Join-Path $out 'EAHideConnectorLabels.dll'
New-Item -ItemType Directory -Force $out | Out-Null
if (-not (Test-Path "$out\Interop.EA.dll")) { Copy-Item (Join-Path $eaDir 'Interop.EA.dll') $out }

Get-ChildItem $out -Filter '*.old' -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue
if (Test-Path $dll) { Rename-Item $dll ("EAHideConnectorLabels.{0:yyyyMMddHHmmss}.old" -f (Get-Date)) }

& $csc /nologo /target:library /platform:anycpu /optimize+ `
    "/out:$dll" `
    "/reference:$out\Interop.EA.dll" `
    /reference:System.Windows.Forms.dll `
    (Join-Path $root 'src\HideLabelsAddin.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $dll"
