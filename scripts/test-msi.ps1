#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
. (Join-Path $PSScriptRoot 'packaging/Msi.ps1')
$checks=0
function Check([bool]$Value,[string]$Message) { if(!$Value) { throw $Message }; $script:checks++ }
function Rejected([scriptblock]$Action) { $failed=$false;try { & $Action | Out-Null } catch { $failed=$true }; Check $failed 'Expected MSI input rejection.' }
Assert-ProcSightMsiVersion '0.1.0'
Assert-ProcSightMsiVersion '255.255.65534'
$checks+=2
$msiInputs=Get-ProcSightPackageInputs -Root (Split-Path $PSScriptRoot -Parent) -Profile Msi
Check ($msiInputs.InstallerVersion -eq $msiInputs.ProductVersion) 'MSI uses the central three-component product version.'
Check $msiInputs.AllowsElevation 'Explicit administrator restart remains available.'
Rejected { Get-ProcSightPackageInputs -Root (Split-Path $PSScriptRoot -Parent) -Profile Msi -VersionForValidation '256.0.0' }
Rejected { Get-ProcSightPackageInputs -Root (Split-Path $PSScriptRoot -Parent) -Profile Msi -ElevationMode Disabled }
foreach($version in @('256.0.0','1.256.0','1.0.65535','1.0.0-preview','1.0.0.0')) { Rejected { Assert-ProcSightMsiVersion $version } }
$first=Get-ProcSightMsiGuid 'component/assets/icon.png'
Check ($first -eq (Get-ProcSightMsiGuid 'component/assets/icon.png')) 'Component identity must be stable.'
Check ($first -ne (Get-ProcSightMsiGuid 'component/assets/other.png')) 'Different files need different component identities.'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ProcSight-MsiTests-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path (Join-Path $fixture 'payload/Assets/Package') -Force
try {
 $payload=Join-Path $fixture 'payload'
 foreach($name in @('ProcSight.App.exe','ProcSight.App.dll','ProcSight-LICENSE.txt','ProcSight-THIRD-PARTY-NOTICES.txt','Assets/Package/StoreLogo.png')) { [IO.File]::WriteAllText((Join-Path $payload $name),'Synthetic fixture '+$name) }
 [IO.File]::WriteAllText((Join-Path $payload 'settings.json'),'sensitive fixture')
 Rejected { Get-ProcSightMsiPayload $payload }
 Remove-Item -LiteralPath (Join-Path $payload 'settings.json')
 $files=@(Get-ProcSightMsiPayload $payload)
 Check ($files.Count -eq 5) 'All approved payload files must be included.'
 Check (@($files | Select-Object -ExpandProperty Id -Unique).Count -eq 5) 'File identifiers must be unique.'
 $msi=Join-Path $fixture 'synthetic.msi'
 New-ProcSightMsi -Folder $payload -Path $msi -Version '0.1.0' -Manufacturer 'Synthetic Fixture'
 $inspection=Read-ProcSightMsi $msi
 Check ($inspection.Properties.ProductVersion -eq '0.1.0') 'MSI version must match source.'
 Check ($inspection.Properties.ALLUSERS -eq '1') 'Installation must use the protected per-machine location.'
 Check ($inspection.Template -eq 'x64;1033') 'MSI must declare x64.'
 Check ($inspection.Files.Count -eq 5) 'MSI file table must match the payload.'
 Check ($inspection.CustomActionCount -eq 0) 'Installer must not execute custom code.'
 Check ($inspection.ShortcutTarget -eq 'Main') 'Advertised shortcut must resolve the main feature/executable key path.'
 Check ($inspection.UpgradeCount -eq 2) 'Both older-version removal and newer-version detection must exist.'
 Check ($inspection.LaunchConditions -match '26100') 'OS build floor must be enforced.'
 Check (@($inspection.Components | Where-Object { [int]$_.Attributes -ne 256 }).Count -eq 0) 'Every file component must be 64-bit.'
 Assert-ProcSightMsiContents $msi $payload '0.1.0' (Join-Path $fixture 'inspection')
 $checks++
 $productCode=$inspection.Properties.ProductCode
 Check ($inspection.Properties.UpgradeCode -eq (Get-ProcSightMsiGuid 'upgrade/x64')) 'Upgrade identity must remain stable.'
 Check ($productCode -ne (Get-ProcSightMsiGuid 'product/x64/0.2.0')) 'A new product version needs a new product code.'
 Write-Output "MSI checks passed: $checks (synthetic database/cabinet only; no product installed)."
} finally {
 $target=[IO.Path]::GetFullPath($fixture)
 $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
 if(!$target.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($target) -notlike 'ProcSight-MsiTests-*') { throw 'Unsafe MSI fixture cleanup path.' }
 Remove-Item -LiteralPath $target -Recurse -Force
}
