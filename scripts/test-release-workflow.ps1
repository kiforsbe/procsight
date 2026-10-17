param()
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('procsight-release-flow-'+[guid]::NewGuid())
$checks=0
function Run-Git([string[]]$Arguments) { $result=& git -C $fixture @Arguments 2>&1; if($LASTEXITCODE -ne 0) { throw ($result -join "`n") }; return ($result -join "`n").Trim() }
function Check($value,[string]$message) { if(!$value) { throw $message }; $script:checks++ }
function Rejected([scriptblock]$action,[string]$case='') { $failed=$false; try { & $action | Out-Null } catch { $failed=$true; $script:lastRejection=$_.Exception.Message }; Check $failed "Expected workflow to reject $case : $action" }
New-Item -ItemType Directory -Path (Join-Path $fixture 'src/ProcSight.App') -Force | Out-Null
try {
 Copy-Item -LiteralPath (Join-Path $sourceRoot 'scripts') -Destination (Join-Path $fixture 'scripts') -Recurse
 Copy-Item -LiteralPath (Join-Path $sourceRoot 'Version.props') -Destination (Join-Path $fixture 'Version.props')
 foreach($name in @('app.manifest','Package.appxmanifest')) { Copy-Item -LiteralPath (Join-Path $sourceRoot "src/ProcSight.App/$name") -Destination (Join-Path $fixture "src/ProcSight.App/$name") }
 Set-Content -LiteralPath (Join-Path $fixture '.gitignore') -Value '/artifacts/'
 Set-Content -LiteralPath (Join-Path $fixture 'CHANGELOG.md') -Value "# Changes`n`n## [Unreleased]`n`n### Fixed`n`n- Synthetic fix.`n"
 @'
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot '../artifacts/fail-verify')) { throw 'Synthetic verify failure' }
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot '../artifacts/concurrent-edit')) { Add-Content -LiteralPath (Join-Path $PSScriptRoot '../CHANGELOG.md') -Value 'Intervening user edit' }
'@ | Set-Content -LiteralPath (Join-Path $fixture 'scripts/verify.ps1')
 @'
param($Profile,$IdentityFile,$CertificateThumbprint,$ElevationMode,$FeedUri,$PackageUri,$TimestampUri)
$root=Split-Path $PSScriptRoot -Parent
if(Test-Path -LiteralPath (Join-Path $root 'artifacts/fail-package')) { throw 'Synthetic package failure' }
. (Join-Path $PSScriptRoot 'release/Common.ps1')
$version=Read-ProcSightVersion $root; $commit=(& git -C $root rev-parse HEAD).Trim()
$folder=Join-Path $root 'artifacts/packages/fixture'; New-Item -ItemType Directory -Path $folder -Force | Out-Null
$artifact=Join-Path $folder "ProcSight-$version.msix"; Set-Content -LiteralPath $artifact -Value 'synthetic bytes'
$manifest=Join-Path $folder 'release-artifacts.json'
@{Version=$version;PackageVersion="$version.0";Commit=$commit;Artifact=[IO.Path]::GetFileName($artifact);SHA256=(Get-FileHash -LiteralPath $artifact).Hash;Signed=$false;Profile=$Profile} | ConvertTo-Json | Set-Content -LiteralPath $manifest
[pscustomobject]@{ Artifact=$artifact;Manifest=$manifest;Version=$version;Commit=$commit }
'@ | Set-Content -LiteralPath (Join-Path $fixture 'scripts/package.ps1')
 $null=Run-Git @('init','--initial-branch=fixture-release'); $null=Run-Git @('config','user.name','Synthetic Fixture'); $null=Run-Git @('config','user.email','fixture@example.invalid')
 $null=Run-Git @('add','.'); $null=Run-Git @('commit','-m','Synthetic baseline')
 $script=Join-Path $fixture 'scripts/prepare-release.ps1'
 $before=Run-Git @('rev-parse','HEAD')
 $null=Run-Git @('checkout','--detach'); Rejected { & $script -Version 0.1.0 -Profile Direct } 'detached HEAD'; $null=Run-Git @('checkout','fixture-release')
 $savedGitEnvironment=@{}
 foreach($key in @('GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL')) { $savedGitEnvironment[$key]=[Environment]::GetEnvironmentVariable($key); Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue }
 try { $null=Run-Git @('config','user.email',''); Rejected { & $script -Version 0.1.0 -Profile Direct } 'missing Git identity' }
 finally { foreach($key in $savedGitEnvironment.Keys) { if($null -eq $savedGitEnvironment[$key]) { Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue } else { [Environment]::SetEnvironmentVariable($key,$savedGitEnvironment[$key]) } }; $null=Run-Git @('config','user.email','fixture@example.invalid') }
 $null=Run-Git @('tag','v0.1.0'); Rejected { & $script -Version 0.1.0 -Profile Direct } 'existing tag'; $null=Run-Git @('tag','-d','v0.1.0')
 & $script -Version 0.1.0 -Profile Direct -DryRun | Out-Null
 Check ((Run-Git @('status','--porcelain')) -eq '') 'Dry run changed files'
 Check ((Run-Git @('rev-parse','HEAD')) -eq $before) 'Dry run changed HEAD'
 Set-Content -LiteralPath (Join-Path $fixture 'dirty.txt') -Value 'Unrelated file'
 Rejected { & $script -Version 0.1.0 -Profile Direct }; Remove-Item -LiteralPath (Join-Path $fixture 'dirty.txt')
 New-Item -ItemType Directory -Path (Join-Path $fixture 'artifacts') -Force | Out-Null
 Set-Content -LiteralPath (Join-Path $fixture 'artifacts/fail-verify') -Value 'fixture'
 Rejected { & $script -Version 0.1.0 -Profile Direct }
 Check ((Run-Git @('status','--porcelain')) -eq '') 'Failed verification did not restore files'
 Check ((Run-Git @('rev-parse','HEAD')) -eq $before) 'Failed verification changed HEAD'
 Remove-Item -LiteralPath (Join-Path $fixture 'artifacts/fail-verify')
 $originalChangelog=[IO.File]::ReadAllBytes((Join-Path $fixture 'CHANGELOG.md'))
 Set-Content -LiteralPath (Join-Path $fixture 'artifacts/concurrent-edit') -Value 'fixture'
 Rejected { & $script -Version 0.1.1 -Profile Direct } 'intervening inventory edit'
 Check ((Get-Content -LiteralPath (Join-Path $fixture 'CHANGELOG.md') -Raw).Contains('Intervening user edit')) 'Rollback lost an intervening edit'
 Check ((Run-Git @('rev-parse','HEAD')) -eq $before) 'Intervening edit was committed'
 [IO.File]::WriteAllBytes((Join-Path $fixture 'CHANGELOG.md'),$originalChangelog)
 Remove-Item -LiteralPath (Join-Path $fixture 'artifacts/concurrent-edit')
 Set-Content -LiteralPath (Join-Path $fixture 'artifacts/fail-package') -Value 'fixture'
 Rejected { & $script -Version 0.1.0 -Profile Direct }
 Check ((Run-Git @('log','-1','--format=%s')) -eq 'chore: release v0.1.0') "Missing prepared release commit: $script:lastRejection"
 Check ((Run-Git @('tag','--list')) -eq '') 'Failed package created a tag'
 Remove-Item -LiteralPath (Join-Path $fixture 'artifacts/fail-package')
 & $script -Resume -Version 0.1.0 -Profile Direct | Out-Null
 Check ((Run-Git @('tag','--list')) -eq 'v0.1.0') 'Resume did not tag release'
 $releaseCommit=Run-Git @('rev-parse','HEAD')
 Check ((Run-Git @('rev-parse','v0.1.0^{}')) -eq $releaseCommit) 'Tag does not point to release commit'
 Check ((Run-Git @('cat-file','-t','v0.1.0')) -eq 'tag') 'Release tag is not annotated'
 & $script -Resume -Version 0.1.0 -Profile Direct | Out-Null
 Check ((Run-Git @('rev-parse','HEAD')) -eq $releaseCommit) 'Idempotent resume made another commit'
 $null=Run-Git @('commit','--allow-empty','-m','Unrelated subsequent work')
 Rejected { & $script -Resume -Version 0.1.0 -Profile Direct }
 Check ((Run-Git @('rev-parse','v0.1.0^{}')) -eq $releaseCommit) 'Mismatched resume moved the release tag'
 Rejected { & $script -Version 0.0.9 -Profile Direct }
 Check ((Get-Content -LiteralPath (Join-Path $fixture 'CHANGELOG.md') -Raw).Contains('[0.1.0] -')) 'Release changelog was not created'
 Write-Output "Release workflow checks passed: $checks"
} finally {
 if([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
