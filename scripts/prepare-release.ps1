[CmdletBinding(DefaultParameterSetName='Version')]
param(
 [Parameter(Mandatory,ParameterSetName='Version')][Parameter(Mandatory,ParameterSetName='Resume')][string]$Version,
 [Parameter(Mandatory,ParameterSetName='Bump')][ValidateSet('Major','Minor','Patch')][string]$Bump,
 [Parameter(Mandatory,ParameterSetName='Resume')][switch]$Resume,
 [Parameter(Mandatory)][ValidateSet('Direct','Store')][string]$Profile,
 [switch]$DryRun,[string]$ReleaseDate=(Get-Date -Format 'yyyy-MM-dd'),
 [string]$IdentityFile,[string]$CertificateThumbprint,[ValidateSet('Disabled','Approved')][string]$ElevationMode,
 [string]$FeedUri,[string]$PackageUri,[string]$TimestampUri)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
$root=Split-Path $PSScriptRoot -Parent
function Invoke-ReleaseGit([string[]]$Arguments) {
 $result=@(& git -C $root -c core.safecrlf=false @Arguments 2>&1); $code=$LASTEXITCODE
 if($code -ne 0) { throw "Git $($Arguments[0]) failed; no history was rewritten." }
 return ($result -join "`n").Trim()
}
function Assert-CleanReleaseCheckout {
 if(Invoke-ReleaseGit @('status','--porcelain','--untracked-files=all')) { throw 'Release preparation requires a clean checkout and index, including untracked source files.' }
}
Assert-CleanReleaseCheckout
$null=Invoke-ReleaseGit @('symbolic-ref','--quiet','--short','HEAD')
foreach($identityVariable in @('GIT_AUTHOR_IDENT','GIT_COMMITTER_IDENT')) {
 $identity=Invoke-ReleaseGit @('var',$identityVariable)
 if($identity -notmatch '^[^<>]*\S[^<>]* <[^<>\s]+> \d+ [+-]\d{4}$') { throw 'Configure a nonempty Git author and committer identity before preparing a release.' }
}
$date=[datetime]::ParseExact($ReleaseDate,'yyyy-MM-dd',[cultureinfo]::InvariantCulture).ToString('yyyy-MM-dd')
$current=Read-ProcSightVersion $root
$versionArgs=@{Current=$current;Store=($Profile -eq 'Store')}
if($Bump) { $versionArgs.Bump=$Bump } else { $versionArgs.Version=$Version }
$release=Get-ProcSightReleaseVersion @versionArgs
$Version=$release.Version; $tag=$release.Tag
$head=Invoke-ReleaseGit @('rev-parse','HEAD'); $subject=Invoke-ReleaseGit @('log','-1','--format=%s')
$tags=@((Invoke-ReleaseGit @('tag','--list','v*')) -split "`n" | Where-Object { $_ })
$existing=$tag -in $tags
if($existing) {
 if(!$Resume -or (Invoke-ReleaseGit @('rev-parse',"$tag^{}")) -ne $head -or $subject -cne "chore: release $tag" -or (Invoke-ReleaseGit @('cat-file','-t',$tag)) -ne 'tag') {
  throw 'Release tag already exists or does not match this prepared release. Tags are never moved.'
 }
}
if($Resume) {
 if($Version -cne $current -or $subject -cne "chore: release $tag") { throw 'Resume requires the same central version and prepared release commit at HEAD.' }
} else {
 if($subject -match '^chore: release (v[0-9]+\.[0-9]+\.[0-9]+)$' -and $Matches[1] -notin $tags) { throw 'An untagged prepared release exists. Use -Resume with that version.' }
 if([version]$Version -lt [version]$current -or ($tags.Count -gt 0 -and [version]$Version -le [version]$current)) { throw 'The release version must increase; only the first release can use the initial central version.' }
 foreach($previous in $tags) { if($previous -match '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -and [version]$Version -le [version]$previous.Substring(1)) { throw 'Release version must be greater than existing local release tags.' } }
}
$inputs=Get-ProcSightPackageInputs -Root $root -Profile $Profile -IdentityFile $IdentityFile -ElevationMode $ElevationMode -VersionForValidation $Version
$null=Get-ProcSightSdkTool 'makeappx.exe'
if([bool]$FeedUri -ne [bool]$PackageUri) { throw 'Supply both FeedUri and PackageUri, or neither.' }
if($FeedUri) {
 Assert-ProcSightFeedUri $FeedUri; Assert-ProcSightFeedUri $PackageUri
 if(!$inputs.Production -or $Profile -ne 'Direct' -or !$CertificateThumbprint) { throw 'Update feeds require a signed production Direct package.' }
}
if($CertificateThumbprint) {
 if($Profile -eq 'Store') { throw 'Store preparation must not use a Direct signing certificate.' }
 $null=Get-ProcSightSdkTool 'signtool.exe'
 if(!$DryRun) { $null=Get-ProcSightSigningCertificate $CertificateThumbprint $inputs.Publisher -Production:$inputs.Production }
}
if($inputs.Production -and $Profile -eq 'Direct' -and (!$CertificateThumbprint -or !$TimestampUri)) { throw 'Production Direct release requires trusted signing and TimestampUri.' }
if($TimestampUri) { Assert-ProcSightFeedUri $TimestampUri }
$changelogPath=Join-Path $root 'CHANGELOG.md'; $changelog=[IO.File]::ReadAllText($changelogPath)
if($Resume) {
 if($changelog -notmatch "(?m)^## \[$([regex]::Escape($Version))\] - \d{4}-\d{2}-\d{2}") { throw 'Prepared release changelog is missing.' }
 $newChangelog=$changelog
} else { $newChangelog=New-ProcSightReleaseChangelog $changelog $Version $date }
$targets=@(Get-ProcSightVersionTargets $root)+@('CHANGELOG.md')
if($DryRun) {
 Write-Output "DRY RUN: $current -> $Version; Windows version $($release.WindowsVersion); annotated tag $tag; profile $Profile."
 Write-Output ('Version/changelog inventory: '+($targets -join ', '))
 Write-Output 'Order: preflight -> synchronize -> verify -> release commit -> exact-commit package/sign/inspect -> annotated local tag. No remote publication.'
 return
}
if(!$Resume) {
 $originals=@{}; foreach($target in $targets) { $originals[$target]=[IO.File]::ReadAllBytes((Join-Path $root $target)) }
 $committed=$false; $stagedByUs=$false; $written=@{}
 try {
  # Render every edit before touching source, so a partial write can be rolled back safely.
  $editRoot=Join-Path $root ('artifacts/release-edits/'+[guid]::NewGuid().ToString('N'))
  foreach($target in $targets) {
   $editPath=Join-Path $editRoot $target
   [IO.Directory]::CreateDirectory((Split-Path $editPath -Parent)) | Out-Null
   [IO.File]::WriteAllBytes($editPath,$originals[$target])
  }
  Set-ProcSightProductVersion $editRoot $Version
  [IO.File]::WriteAllText((Join-Path $editRoot 'CHANGELOG.md'),$newChangelog,[Text.UTF8Encoding]::new($false))
  foreach($target in $targets) { $written[$target]=[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $editRoot $target))) }
  foreach($target in $targets) { [IO.File]::WriteAllBytes((Join-Path $root $target),[Convert]::FromBase64String($written[$target])) }
  Assert-ProcSightVersionConsistency $root
  & (Join-Path $PSScriptRoot 'verify.ps1')
  if(!$?) { throw 'Release verification failed.' }
  if((Invoke-ReleaseGit @('rev-parse','HEAD')) -ne $head) { throw 'HEAD changed during release preparation.' }
  foreach($target in $targets) {
   if([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $target))) -cne $written[$target]) { throw 'Release inventory changed during verification; preserving intervening edits.' }
  }
  $changes=@((Invoke-ReleaseGit @('diff','--name-only')) -split "`n" | Where-Object { $_ })
  if(@($changes | Where-Object { $_ -notin $targets }).Count -gt 0 -or (Invoke-ReleaseGit @('diff','--cached','--name-only')) -or (Invoke-ReleaseGit @('ls-files','--others','--exclude-standard'))) { throw 'Unrelated edits appeared during verification; refusing to commit them.' }
  $null=Invoke-ReleaseGit (@('add','--')+$targets)
  $stagedByUs=$true
  $null=Invoke-ReleaseGit @('diff','--cached','--check')
  $null=Invoke-ReleaseGit @('commit','-m',"chore: release $tag")
  $committed=$true; $head=Invoke-ReleaseGit @('rev-parse','HEAD')
 } catch {
  if(!$committed -and (Invoke-ReleaseGit @('rev-parse','HEAD')) -eq $head) {
   foreach($target in $targets) {
    $targetPath=Join-Path $root $target
    if($written.ContainsKey($target) -and (Test-Path -LiteralPath $targetPath) -and
     [Convert]::ToBase64String([IO.File]::ReadAllBytes($targetPath)) -ceq $written[$target]) { [IO.File]::WriteAllBytes($targetPath,$originals[$target]) }
   }
   # Unstage only the release inventory; do not reset history or unrelated files.
   if($stagedByUs) { $null=Invoke-ReleaseGit (@('restore','--staged','--')+$targets) }
  }
  throw
 }
}
Assert-CleanReleaseCheckout
Assert-ProcSightVersionConsistency $root
$packageArgs=@{Profile=$Profile}
foreach($key in @('IdentityFile','CertificateThumbprint','ElevationMode','FeedUri','PackageUri','TimestampUri')) {
 if($PSBoundParameters.ContainsKey($key)) { $packageArgs[$key]=$PSBoundParameters[$key] }
}
try {
 $results=@(& (Join-Path $PSScriptRoot 'package.ps1') @packageArgs)
 $packages=@($results | Where-Object { $_.PSObject.Properties.Name -contains 'Artifact' -and $_.PSObject.Properties.Name -contains 'Manifest' })
 if($packages.Count -ne 1) { throw 'Package command did not return exactly one inspected artifact.' }
 $package=$packages[0]; $metadata=Get-Content -LiteralPath $package.Manifest -Raw | ConvertFrom-Json
 if($metadata.Version -cne $Version -or $metadata.PackageVersion -cne $release.WindowsVersion -or $metadata.Commit -cne $head -or
  $metadata.SHA256 -cne (Get-FileHash -LiteralPath $package.Artifact -Algorithm SHA256).Hash) { throw 'Release artifact version/commit/hash does not agree with the release commit.' }
 Assert-CleanReleaseCheckout
 if((Invoke-ReleaseGit @('rev-parse','HEAD')) -ne $head) { throw 'HEAD changed while release packages were built.' }
 $releaseFolder=Join-Path $root "artifacts/releases/$Version"; New-Item -ItemType Directory -Path $releaseFolder -Force | Out-Null
 $section=[regex]::Match($newChangelog,"(?ms)^## \[$([regex]::Escape($Version))\][^\r\n]*\r?\n(?<body>.*?)(?=^## \[|\z)").Groups['body'].Value.Trim()
 [IO.File]::WriteAllText((Join-Path $releaseFolder "release-notes-$Version.md"),"# ProcSight $Version`n`n$section`n`nSource commit: $head`n",[Text.UTF8Encoding]::new($false))
 [pscustomobject]@{Version=$Version;Tag=$tag;Commit=$head;Artifact=$package.Artifact;SHA256=$metadata.SHA256;Profile=$Profile} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseFolder 'release.json') -Encoding utf8NoBOM
 if(!$existing) { $null=Invoke-ReleaseGit @('tag','-a',$tag,$head,'-m',"ProcSight $Version") }
 Write-Output "Prepared $tag at $head. Artifact: $($package.Artifact)"
 Write-Output 'Local commit/tag only. Nothing was pushed, uploaded or published.'
} catch { throw "Release commit $head remains prepared; no tag was created or moved by the failed step. Retry with -Resume -Version $Version -Profile $Profile. $($_.Exception.Message)" }
