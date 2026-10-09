# Registers the add-in for the current user only (no admin rights needed).
# Restart Enterprise Architect afterwards.
$ErrorActionPreference = 'Stop'
$root   = $PSScriptRoot
$dll    = Join-Path $root 'bin\EAHideConnectorLabels.dll'
$regasm = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe'   # 32-bit, matches 32-bit EA
if (-not (Test-Path $dll)) { & (Join-Path $root 'build.ps1') }

# Let RegAsm describe the COM registration, then import it under HKCU instead of HKCR.
$tmp = Join-Path $env:TEMP 'EAHideConnectorLabels.reg'
& $regasm /nologo /codebase $dll "/regfile:$tmp" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "RegAsm failed" }
(Get-Content $tmp -Raw) -replace 'HKEY_CLASSES_ROOT', 'HKEY_CURRENT_USER\Software\Classes' |
    Set-Content $tmp -Encoding Unicode
& reg.exe import $tmp /reg:32 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "reg import failed" }
Remove-Item $tmp

# Tell EA to load it.
New-Item -Force 'HKCU:\Software\Sparx Systems\EAAddins\EAHideConnectorLabels' |
    Set-ItemProperty -Name '(default)' -Value 'EAHideConnectorLabels.Addin'
Write-Host 'Installed. Restart Enterprise Architect.'
