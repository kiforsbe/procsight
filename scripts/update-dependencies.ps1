#requires -Version 7.0
[CmdletBinding()]
param([switch]$Apply)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'dependencies/Common.ps1')
$root=Split-Path $PSScriptRoot -Parent
$central=Join-Path $root 'Directory.Packages.props'
$null=Invoke-ProcSightDependencyDotnet $root @('restore','ProcSight.slnx','--locked-mode','--nologo')
$outdated=Get-ProcSightDependencyReport $root outdated
$vulnerable=Get-ProcSightDependencyReport $root vulnerable
$deprecated=Get-ProcSightDependencyReport $root deprecated
$outdatedRows=@(Get-ProcSightDependencyRows $outdated)
$vulnerableRows=@(Get-ProcSightDependencyRows $vulnerable)
$deprecatedRows=@(Get-ProcSightDependencyRows $deprecated)
Write-Host "Review: $($outdatedRows.Count) outdated, $($vulnerableRows.Count) vulnerable, $($deprecatedRows.Count) deprecated project/package occurrences (including transitives)."
[xml]$versions=Get-Content -LiteralPath $central -Raw
$approved=@{}
foreach($node in $versions.Project.ItemGroup.PackageVersion) { $approved[$node.Include]=$node.Version }
$updates=@{}
foreach($row in $outdatedRows) {
 if(!$approved.ContainsKey($row.id)) {
  Write-Warning "$($row.id): $($row.resolvedVersion) -> $($row.latestVersion) needs an explicit central pin or parent-package decision."
  continue
 }
 if($updates.ContainsKey($row.id) -and $updates[$row.id].Version -ne $row.latestVersion) { throw "Conflicting candidate versions for $($row.id)." }
 $metadata=Get-ProcSightDependencyMetadata $row.id $row.latestVersion
 Assert-ProcSightDependencyMetadata $row.id $row.latestVersion $metadata
 $updates[$row.id]=[pscustomobject]@{Id=$row.id;Current=$approved[$row.id];Version=$row.latestVersion}
}
if($updates.Count) { $updates.Values | Sort-Object Id | Format-Table Id,Current,Version | Out-Host }
foreach($entry in Get-ProcSightDependencySelection $root) {
 if($updates.ContainsKey($entry.Id)) { continue } # The candidate replaces this old version; validate the final graph after restore.
 Assert-ProcSightDependencyMetadata $entry.Id $entry.Version (Get-ProcSightDependencyMetadata $entry.Id $entry.Version)
}
if(!$Apply) {
 Write-Host 'Review only. Use -Apply to update approved central versions and run validation. License/support compatibility still requires review; no files changed.'
 return
}
# Refuse to overwrite the caller's dependency edits. Other workspace files are preserved.
$relativePaths=@('Directory.Packages.props')+@(& git -C $root ls-files -- 'src/*/packages.lock.json' 'tests/*/packages.lock.json')
if($LASTEXITCODE -ne 0) { throw 'Cannot enumerate tracked dependency files.' }
$dirty=@(& git -C $root status --porcelain -- @relativePaths)
if($LASTEXITCODE -ne 0 -or $dirty.Count) { throw 'Commit or preserve existing dependency-file edits before applying updates.' }
if(!$updates.Count) {
 if($outdatedRows.Count -or $vulnerableRows.Count -or $deprecatedRows.Count) { throw 'Dependency findings need a manual migration/pin decision.' }
 Write-Host 'No dependency updates are available.'
 return
}
$paths=@($relativePaths | ForEach-Object { Join-Path $root $_ })
try {
 Invoke-ProcSightDependencyTransaction $paths {
 Set-ProcSightDependencyVersions $central @($updates.Values)
 $null=Invoke-ProcSightDependencyDotnet $root @('restore','ProcSight.slnx','--force-evaluate','--nologo')
 foreach($kind in @('outdated','vulnerable','deprecated')) {
  $remaining=@(Get-ProcSightDependencyRows (Get-ProcSightDependencyReport $root $kind))
  if($remaining.Count) { throw "Resolved graph still has $kind findings; explicit dependency review is required." }
 }
 foreach($entry in Get-ProcSightDependencySelection $root) {
  Assert-ProcSightDependencyMetadata $entry.Id $entry.Version (Get-ProcSightDependencyMetadata $entry.Id $entry.Version)
 }
 $null=Invoke-ProcSightDependencyDotnet $root @('restore','ProcSight.slnx','--locked-mode','--nologo')
 $null=Invoke-ProcSightDependencyDotnet $root @('build','ProcSight.slnx','-c','Release','--no-restore','--nologo')
 # Never automate actual account authentication. Fake-provider tests remain included.
 $null=Invoke-ProcSightDependencyDotnet $root @('test','ProcSight.slnx','-c','Release','--no-build','--no-restore','--filter','FullyQualifiedName!~ChatGptAuth','--nologo')
 Write-Host 'Dependency files updated; graph checks, locked restore, Release build and permitted tests passed. Review licenses/release notes, native UI and packaging before committing. No commit, tag, push or publication was performed.'
 }
} catch {
 $failure=$_
 try { $null=Invoke-ProcSightDependencyDotnet $root @('restore','ProcSight.slnx','--locked-mode','--nologo') }
 catch { Write-Warning 'Dependency files were restored, but generated assets could not be refreshed. Run a locked restore before building.' }
 throw $failure
}
