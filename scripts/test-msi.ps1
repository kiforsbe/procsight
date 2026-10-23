#requires -Version 7.4
# Consumer contract tests; reusable authoring/mutation cases belong to the pinned
# engine's own suite. No installation, removal or shutdown transaction is invoked.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
$root=Split-Path $PSScriptRoot -Parent
$tools=Get-ProcSightBuildToolsRoot $root
foreach($helper in @('Configuration','Identity','Payload','Database','Scope','Maintenance','Semantics','Inspection')) { . (Join-Path $tools "msi/$helper.ps1") }
$checks=0
function Check([bool]$Value,[string]$Message) { if(!$Value) { throw $Message }; $script:checks++ }
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ProcSight-MsiTests-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path "$fixture/payload/Assets/Package" -Force
try {
 foreach($name in @('ProcSight.App.exe','ProcSight.App.dll','ProcSight-LICENSE.txt','ProcSight-THIRD-PARTY-NOTICES.txt','Assets/Package/StoreLogo.png')) {
  [IO.File]::WriteAllText((Join-Path "$fixture/payload" $name),'Synthetic fixture '+$name)
 }
 $originalRegistrations=@(& { $engine=New-Object -ComObject WindowsInstaller.Installer; try { $engine.Products | Sort-Object } finally { Close-MsiCom $engine } }) -join ','
 $result=New-ProcSightInstaller -Root $root -Folder "$fixture/payload" -Version '1.2.3' -BuildRoot $fixture
 $actual=Read-MsiPackage $result.Artifact
 Check ($actual.Properties.ProductCode -ceq '{74D67FBA-C97A-3ECA-1D24-0AB1D4F99C56}') 'Shared MSI preserves ProcSight ProductCode.'
 Check ($actual.Properties.UpgradeCode -ceq '{4DF99DE9-8E63-E6C5-2DC5-4E08D8C0D228}') 'Shared MSI preserves existing upgrade family.'
 Check (@($actual.Components | Where-Object ComponentId -CEQ '{5E984AEF-BC0B-4417-66BE-672C1DB0C909}').Count -eq 1) 'Shared MSI preserves existing executable component identity.'
 Check ($actual.Files.Count -eq 5 -and $actual.Components.Count -eq 5) 'Approved payload has one component per file.'
 Check (($actual.Directories | Where-Object Directory -CEQ 'INSTALLFOLDER').DefaultDir.Split('|')[-1] -ceq 'ProcSight') 'Installer does not rename product directory.'
 Check (($actual.Directories | Where-Object Directory -CEQ 'APPMENUDIR').DefaultDir.Split('|')[-1] -ceq 'ProcSight') 'Installer does not rename Start-menu directory.'
 Check ($actual.Properties.ALLUSERS -ceq '2' -and $actual.Properties.MSIINSTALLPERUSER -ceq '1') 'Fresh installations offer user/machine scope with Just me initially selected.'
 Check ($actual.Properties.MSIRESTARTMANAGERCONTROL -ceq 'Disable') 'Application shutdown policy is consumer-owned.'
 Check ($actual.CustomActionCount -eq 0 -and 'CustomAction' -notin $actual.Tables) 'No executable custom actions.'
 Check ($actual.LaunchConditions -match '26100') 'Windows 11 24H2 floor remains enforced.'
 Check ($actual.ShortcutTarget -ceq 'Main') 'Start-menu shortcut remains advertised.'
 $afterRegistrations=@(& { $engine=New-Object -ComObject WindowsInstaller.Installer; try { $engine.Products | Sort-Object } finally { Close-MsiCom $engine } }) -join ','
 Check ($afterRegistrations -ceq $originalRegistrations) 'Packaging/preflight leaves installed product registrations unchanged.'
 Write-Host "MSI consumer checks passed: $checks (no product installed)."
} finally {
 $target=[IO.Path]::GetFullPath($fixture)
 if([IO.Path]::GetDirectoryName($target) -cne [IO.Path]::GetTempPath().TrimEnd('\') -or [IO.Path]::GetFileName($target) -notlike 'ProcSight-MsiTests-*') { throw 'Unsafe MSI fixture cleanup.' }
 Remove-Item -LiteralPath $target -Recurse -Force
}
