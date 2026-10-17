#requires -Version 7.0
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'release/Common.ps1')
$checks=0
function Assert-Equal($actual,$expected) { if($actual -cne $expected) { throw "Expected '$expected', got '$actual'" }; $script:checks++ }
function Assert-Rejected([scriptblock]$action) { $rejected=$false; try { & $action | Out-Null } catch { $rejected=$true }; if(!$rejected) { throw 'Expected rejection' }; $script:checks++ }
Assert-Equal (Get-ProcSightReleaseVersion -Current '0.1.0' -Bump Patch).Version '0.1.1'
Assert-Equal (Get-ProcSightReleaseVersion -Current '1.2.3' -Bump Minor).Version '1.3.0'
Assert-Equal (Get-ProcSightReleaseVersion -Current '1.2.3' -Bump Major).Tag 'v2.0.0'
Assert-Equal (Get-ProcSightReleaseVersion -Current '0.1.0' -Version '1.2.3').WindowsVersion '1.2.3.0'
foreach($invalid in @('01.2.3','1.2','1.2.3.4','1.2.3-rc.1','1.2.3+build','65536.0.0','1.-1.0')) {
 Assert-Rejected { Get-ProcSightReleaseVersion -Current '0.1.0' -Version $invalid }
}
Assert-Rejected { Get-ProcSightReleaseVersion -Current '0.1.0' -Version '0.2.0' -Store }
Assert-Rejected { Get-ProcSightReleaseVersion -Current '0.1.0' }
Assert-Rejected { Get-ProcSightReleaseVersion -Current '0.1.0' -Version '1.0.0' -Bump Patch }
$root=Join-Path ([IO.Path]::GetTempPath()) ('procsight-release-tests-'+[guid]::NewGuid())
New-Item -ItemType Directory -Path (Join-Path $root 'src/ProcSight.App') -Force | Out-Null
try {
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../Version.props') -Destination (Join-Path $root 'Version.props')
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../src/ProcSight.App/app.manifest') -Destination (Join-Path $root 'src/ProcSight.App/app.manifest')
 Set-Content -LiteralPath (Join-Path $root 'history.md') -Value 'Example 0.1.0. Dependency 2.2.1.'
 Assert-ProcSightVersionConsistency $root
 Set-ProcSightProductVersion $root '1.2.3'
 Assert-Equal (Read-ProcSightVersion $root) '1.2.3'
 Assert-ProcSightVersionConsistency $root
 Assert-Equal (Get-Content -LiteralPath (Join-Path $root 'history.md') -Raw).Trim() 'Example 0.1.0. Dependency 2.2.1.'
 [xml]$manifest=Get-Content -LiteralPath (Join-Path $root 'src/ProcSight.App/app.manifest') -Raw
 $manifest.assembly.assemblyIdentity.SetAttribute('version','9.9.9.0'); $manifest.Save((Join-Path $root 'src/ProcSight.App/app.manifest'))
 Assert-Rejected { Assert-ProcSightVersionConsistency $root }
 $notes=New-ProcSightReleaseChangelog "# Changes`n`n## [Unreleased]`n`n### Fixed`n`n- Example fix.`n" '1.2.3' '2026-10-17'
 Assert-Equal ($notes.Contains('## [1.2.3] - 2026-10-17')) $true
 Assert-Equal ($notes.Contains('- Example fix.')) $true
 Assert-Rejected { New-ProcSightReleaseChangelog "## [Unreleased]`n" '1.2.3' '2026-10-17' }
} finally {
 # Only this explicitly created temporary fixture directory is removed.
 if([IO.Path]::GetFullPath($root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $root -Recurse -Force }
}
Write-Output "Release checks passed: $checks"
