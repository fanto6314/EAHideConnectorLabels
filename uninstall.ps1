# Removes the add-in registration for the current user. Restart Enterprise Architect afterwards.
$clsid = '{6F3B2C1A-8E4D-4B7A-9C21-5D0E7A4F1B63}'
& reg.exe delete 'HKCU\Software\Sparx Systems\EAAddins\EAHideConnectorLabels' /f 2>$null | Out-Null
& reg.exe delete "HKCU\Software\Classes\CLSID\$clsid" /f /reg:32 2>$null | Out-Null
& reg.exe delete 'HKCU\Software\Classes\EAHideConnectorLabels.Addin' /f /reg:32 2>$null | Out-Null
& reg.exe delete 'HKCU\Software\EAHideConnectorLabels' /f 2>$null | Out-Null
Write-Host 'Uninstalled. Restart Enterprise Architect.'
