#requires -Version 7.0
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'dependencies/Common.ps1')
$checks=0
function Assert-True([bool]$Condition,[string]$Message) {
 if(!$Condition) { throw $Message }; $script:checks++
}
function Assert-Rejected([scriptblock]$Action) {
 $rejected=$false
 try { & $Action } catch { $rejected=$true }
 Assert-True $rejected 'Expected dependency rejection.'
}
Assert-ProcSightDependencyMetadata 'Example' '1.2.3' ([pscustomobject]@{listed=$true})
$checks++
Assert-Rejected { Assert-ProcSightDependencyMetadata 'Example' '1.2.3-preview' ([pscustomobject]@{listed=$true}) }
Assert-Rejected { Assert-ProcSightDependencyMetadata 'Example' '1.2.3' ([pscustomobject]@{listed=$false}) }
Assert-Rejected { Assert-ProcSightDependencyMetadata 'Example' '1.2.3' ([pscustomobject]@{}) }
Assert-Rejected { Assert-ProcSightDependencyMetadata 'Example' '1.2.3' ([pscustomobject]@{listed=$true;deprecation=@{reasons=@('Legacy')}}) }
Assert-Rejected { Assert-ProcSightDependencyMetadata 'Example' '1.2.3' ([pscustomobject]@{listed=$true;vulnerabilities=@(@{severity=2})}) }
$report=@{projects=@(@{path='empty'},@{frameworks=@(@{topLevelPackages=@(@{id='A'});transitivePackages=@(@{id='B'})})})}
Assert-True (@(Get-ProcSightDependencyRows $report).Count -eq 2) 'Both direct and transitive rows must be inspected.'
Assert-ProcSightVulnerabilityReport @{version=1;projects=@(@{path='example'})}
$checks++
Assert-Rejected { Assert-ProcSightVulnerabilityReport @{version=1;projects=@(@{frameworks=@(@{transitivePackages=@(@{id='Unsafe';vulnerabilities=@(@{severity='Low'})})})})} }
Assert-Rejected { Assert-ProcSightVulnerabilityReport @{version=1;projects=@(@{frameworks=@(@{topLevelPackages=@(@{id='Unsafe'})})})} }
Assert-Rejected { Assert-ProcSightVulnerabilityReport @{version=1;projects=@()} }
Assert-Rejected { Assert-ProcSightVulnerabilityReport @{projects=@(@{path='example'})} }
$auditArguments=Get-ProcSightPackageAuditArguments
Assert-True ('-p:NuGetAuditMode=all' -in $auditArguments -and '-p:NuGetAuditLevel=low' -in $auditArguments) 'Audit all transitive packages at every severity.'
Assert-True (($auditArguments | Where-Object { $_ -like '-p:WarningsAsErrors=*' }) -match 'NU1900.*NU1901.*NU1902.*NU1903.*NU1904.*NU1905') 'Advisory retrieval errors and vulnerabilities must fail restore.'
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('ProcSight-DependencyTests-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $temporary
$central=Join-Path $temporary 'Directory.Packages.props'; $lock=Join-Path $temporary 'packages.lock.json'
try {
 [IO.File]::WriteAllText($central,'<Project><ItemGroup><PackageVersion Include="Example" Version="1.0.0" /></ItemGroup></Project>')
 [IO.File]::WriteAllText($lock,'original lock')
 $before=[Convert]::ToBase64String([IO.File]::ReadAllBytes($central))
 Assert-Rejected {
  Invoke-ProcSightDependencyTransaction @($central,$lock) {
   Set-ProcSightDependencyVersions $central @([pscustomobject]@{Id='Example';Version='2.0.0'})
   [IO.File]::WriteAllText($lock,'changed lock')
   throw 'Synthetic compatibility failure'
  }
 }
 Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($central)) -eq $before) 'Restore central file byte-for-byte after failure.'
 Assert-True ([IO.File]::ReadAllText($lock) -eq 'original lock') 'Restore lock after failure.'
 Assert-Rejected { Set-ProcSightDependencyVersions $central @([pscustomobject]@{Id='Unapproved';Version='2.0.0'}) }
 Invoke-ProcSightDependencyTransaction @($central,$lock) { Set-ProcSightDependencyVersions $central @([pscustomobject]@{Id='Example';Version='2.0.0'}) }
 Assert-True (([xml][IO.File]::ReadAllText($central)).Project.ItemGroup.PackageVersion.Version -eq '2.0.0') 'Retain successful updates.'
} finally {
 foreach($path in @($central,$lock)) { if(Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
 Remove-Item -LiteralPath $temporary
}
Write-Host "Dependency helper checks passed: $checks (synthetic, no registry calls or package changes)."
