#requires -Version 7.0
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
$checks=0
function Assert-Equal($actual,$expected) { if($actual -cne $expected) { throw "Expected '$expected', got '$actual'" }; $script:checks++ }
function Assert-Rejected([scriptblock]$action) { $failed=$false; try { & $action | Out-Null } catch { $failed=$true }; if(!$failed) { throw 'Expected rejection' }; $script:checks++ }
$root=Split-Path $PSScriptRoot -Parent
$inputs=Get-ProcSightPackageInputs -Root $root -Profile Direct
Assert-Equal $inputs.Name 'ProcSight.Development'
Assert-Equal $inputs.Publisher 'CN=ProcSight Development'
Assert-Equal $inputs.Version ((Read-ProcSightVersion $root)+'.0')
Assert-Rejected { Get-ProcSightPackageInputs -Root $root -Profile Store }
Assert-Rejected { Assert-ProcSightFeedUri 'http://example.com/app.msix' }
Assert-Rejected { Assert-ProcSightFeedUri 'https://user:secret@example.com/app.msix' }
Assert-Rejected { Assert-ProcSightFeedUri 'https://example.com/app.msix#fragment' }
Assert-ProcSightFeedUri 'https://example.com/app.msix?a=1&b=2'
Assert-Equal (Test-ProcSightPackageFile 'ProcSight.App.exe') $true
foreach($name in @('knowledge.db','settings.json','private.pfx','test.log','src/code.cs','.git/config','local-config/config.json','ProcSight.App.pdb')) {
 Assert-Equal (Test-ProcSightPackageFile $name) $false
}
Assert-Equal (Test-ProcSightPackageFile 'machine-inventory.json') $false
Assert-Equal (Test-ProcSightPackageFile 'Microsoft.UI.Xaml/Assets/map.html') $true
Assert-Equal (Test-ProcSightPackageFile 'map.html') $false
Assert-Equal (Test-ProcSightPackageFile 'Microsoft.UI.Xaml/Assets/settings.json') $false
$certificate=[pscustomobject]@{Subject='CN=Fixture';HasPrivateKey=$true;NotBefore=(Get-Date).AddDays(-1);NotAfter=(Get-Date).AddDays(1);UsageOids=@('1.3.6.1.5.5.7.3.3')}
Assert-ProcSightSigningMetadata $certificate 'CN=Fixture'
Assert-Rejected { Assert-ProcSightSigningMetadata $certificate 'CN=Another publisher' }
$certificate.HasPrivateKey=$false; Assert-Rejected { Assert-ProcSightSigningMetadata $certificate 'CN=Fixture' }; $certificate.HasPrivateKey=$true
$certificate.NotAfter=(Get-Date).AddDays(-1); Assert-Rejected { Assert-ProcSightSigningMetadata $certificate 'CN=Fixture' }; $certificate.NotAfter=(Get-Date).AddDays(1)
$certificate.UsageOids=@(); Assert-Rejected { Assert-ProcSightSigningMetadata $certificate 'CN=Fixture' }
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('procsight-package-tests-'+[guid]::NewGuid())
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
 $feedPath=Join-Path $fixture 'update feed.appinstaller'
 New-ProcSightAppInstaller $inputs 'https://example.com/feed?a=1&b=2' 'https://example.com/package?a=1&b=2' $feedPath
 [xml]$feed=Get-Content -LiteralPath $feedPath -Raw
 Assert-Equal $feed.AppInstaller.MainPackage.Uri 'https://example.com/package?a=1&b=2'
 Assert-Equal $feed.AppInstaller.UpdateSettings.OnLaunch.UpdateBlocksActivation 'false'
 Assert-Equal (Test-Path -LiteralPath (Get-ProcSightSdkTool 'makeappx.exe')) $true
} finally {
 if([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
Write-Output "Packaging checks passed: $checks"
