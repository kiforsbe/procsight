#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Direct','Store','Msi')][string]$Profile='Direct',[string]$IdentityFile,[string]$CertificateThumbprint,
 [ValidateSet('Disabled','Approved')][string]$ElevationMode,[string]$FeedUri,[string]$PackageUri,[string]$TimestampUri)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
. (Join-Path $PSScriptRoot 'packaging/Msi.ps1')
. (Join-Path $PSScriptRoot 'dependencies/Common.ps1')
$root=Split-Path $PSScriptRoot -Parent
$inputs=Get-ProcSightPackageInputs -Root $root -Profile $Profile -IdentityFile $IdentityFile -ElevationMode $ElevationMode
$null=Invoke-ProcSightDependencyDotnet $root (@('restore','ProcSight.slnx','--locked-mode','--nologo')+(Get-ProcSightPackageAuditArguments))
Assert-ProcSightVulnerabilityReport (Get-ProcSightDependencyReport $root vulnerable)
if([bool]$FeedUri -ne [bool]$PackageUri) { throw 'Supply both FeedUri and PackageUri, or neither.' }
if($FeedUri) {
 if(!$inputs.Production -or $Profile -ne 'Direct' -or !$CertificateThumbprint) { throw 'An update feed requires a signed production Direct package.' }
 Assert-ProcSightFeedUri $FeedUri; Assert-ProcSightFeedUri $PackageUri
}
$makeappx=if($Profile -ne 'Msi') { Get-ProcSightSdkTool 'makeappx.exe' } else { $null }
$signTool=$null; $certificate=$null
if($CertificateThumbprint) {
 if($Profile -eq 'Store') { throw 'Store preparation is unsigned; Microsoft signs accepted Store submissions.' }
 $signTool=Get-ProcSightSdkTool 'signtool.exe'
 $certificate=Get-ProcSightSigningCertificate $CertificateThumbprint $inputs.Publisher -Production:$inputs.Production
}
if($inputs.Production -and $Profile -eq 'Direct' -and (!$certificate -or !$TimestampUri)) { throw 'Public Direct packages require trusted signing and an explicit HTTPS TimestampUri.' }
if($TimestampUri) { Assert-ProcSightFeedUri $TimestampUri }
$commit=(& git -C $root rev-parse HEAD).Trim(); if($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$buildRoot=Join-Path $root ('artifacts/packages/'+$Profile+'/'+$inputs.ProductVersion+'/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$script:ProcSightToolLog=Join-Path $buildRoot 'tool-output.log'
$generatedManifest=Join-Path $buildRoot 'Package.appxmanifest'
$manifest=[xml]::new(); $manifest.PreserveWhitespace=$true; $manifest.Load((Join-Path $root 'src/ProcSight.App/Package.appxmanifest'))
$manifest.Package.Identity.SetAttribute('Name',$inputs.Name); $manifest.Package.Identity.SetAttribute('Publisher',$inputs.Publisher)
$manifest.Package.Properties.PublisherDisplayName=$inputs.PublisherDisplayName
if(!$inputs.AllowsElevation) {
 foreach($capability in @($manifest.Package.Capabilities.ChildNodes)) { if($capability.NodeType -eq 'Element' -and $capability.GetAttribute('Name') -eq 'allowElevation') { $null=$capability.ParentNode.RemoveChild($capability) } }
}
$manifest.Save($generatedManifest)
$output=Join-Path $buildRoot 'output'; $packages=Join-Path $buildRoot 'msix'
$licenses=Join-Path $buildRoot 'Licenses'
$publish=Join-Path $buildRoot 'publish'
$common=@('-p:ProcSightPackaging=true',"-p:ProcSightPackagingRoot=$buildRoot/","-p:ProcSightPackageProfile=$root/packaging/$Profile.pubxml",'-p:SelfContained=true',
 "-p:WindowsAppSDKSelfContained=$($Profile -ne 'Store')",'-p:Platform=x64',"-p:BaseOutputPath=$output/", "-p:ProcSightPackageManifest=$generatedManifest",
 "-p:AppxPackageDir=$packages/", "-p:SourceRevisionId=$commit","-p:ProcSightLicenseFolder=$licenses",'-p:AppxPackageSigningEnabled=false','-p:AppxAutoIncrementPackageRevision=false')
if($Profile -eq 'Msi') { $common+=@('-p:ProcSightInstallerFormat=Msi','-p:WindowsPackageType=None','-p:EnableMsixTooling=false','-p:DebugType=None','-p:DebugSymbols=false',"-p:PublishDir=$publish/") }
$project=Join-Path $root 'src/ProcSight.App/ProcSight.App.csproj'
Push-Location $root
try {
 Invoke-ProcSightTool 'dotnet' (@('restore',$project,'--locked-mode','--nologo')+(Get-ProcSightPackageAuditArguments)+$common)
 $assets=Get-Content -LiteralPath (Join-Path $buildRoot 'obj/ProcSight.App/project.assets.json') -Raw | ConvertFrom-Json
 foreach($library in $assets.libraries.PSObject.Properties) {
  if($library.Value.type -ne 'package') { continue }
  foreach($packageRoot in $assets.packageFolders.PSObject.Properties.Name) {
   $source=Join-Path $packageRoot $library.Value.path
   if(!(Test-Path -LiteralPath $source)) { continue }
   $noticeFiles=@(Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -match '^(LICENSE|NOTICE|THIRD.PARTY.NOTICES)(\.|$)' })
   if($noticeFiles.Count) {
    $target=Join-Path $licenses ($library.Name.Replace('/','-')); New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach($notice in $noticeFiles) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $target $notice.Name) }
   }
  }
 }
 # Runtime packs are SDK download dependencies rather than ordinary libraries.
 $runtimeInfo=(& dotnet msbuild $project -t:ResolveFrameworkReferences -getItem:ResolvedRuntimePack -nologo @common | Out-String)
 if($LASTEXITCODE -ne 0) { throw 'Cannot inspect resolved runtime pack notices.' }
 foreach($runtime in ($runtimeInfo | ConvertFrom-Json).Items.ResolvedRuntimePack) {
  if(!$runtime.PackageDirectory) { continue }
  $target=Join-Path $licenses ($runtime.Identity+'-'+$runtime.NuGetPackageVersion); New-Item -ItemType Directory -Path $target -Force | Out-Null
  foreach($notice in Get-ChildItem -LiteralPath $runtime.PackageDirectory -File | Where-Object { $_.Name -match '^(LICENSE|NOTICE|THIRD.PARTY.NOTICES)(\.|$)' }) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $target $notice.Name) }
 }
 Invoke-ProcSightTool 'dotnet' (@('publish',$project,'-c','Release','--no-restore','--nologo')+$common)
} finally { Pop-Location }
if($Profile -eq 'Msi') {
 $inspection=$publish
 $suffix=if($certificate){'signed'}else{'unsigned'}
 $artifact=Join-Path $buildRoot "ProcSight-$($inputs.ProductVersion)-x64-$suffix.msi"
 New-ProcSightMsi -Folder $publish -Path $artifact -Version $inputs.ProductVersion
 Assert-ProcSightMsiContents $artifact $publish $inputs.ProductVersion (Join-Path $buildRoot 'inspection')
} else {
 $candidates=@(Get-ChildItem -LiteralPath $packages -Recurse -File -Filter '*.msix' | Where-Object { $_.DirectoryName -notmatch '[\\/]Dependencies([\\/]|$)' })
 if($candidates.Count -ne 1) { throw 'Expected exactly one primary MSIX package.' }
 $inspection=Join-Path $buildRoot 'inspection'
 Invoke-ProcSightTool $makeappx @('unpack','/p',$candidates[0].FullName,'/d',$inspection,'/o')
 Assert-ProcSightPackageContents $inspection $inputs
 $suffix=if($Profile -eq 'Store') {'store-submission'}elseif($certificate){'signed'}else{'unsigned-development'}
 $artifact=Join-Path $buildRoot "ProcSight-$($inputs.ProductVersion)-x64-$suffix.msix"
 Copy-Item -LiteralPath $candidates[0].FullName -Destination $artifact
}
$information=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $inspection 'ProcSight.App.dll')).ProductVersion
if(!$information.StartsWith($inputs.ProductVersion+'+') -or !$information.Contains($commit)) { throw 'Packaged informational version does not identify the source commit.' }
if($certificate) {
 $arguments=@('sign','/sha1',$CertificateThumbprint,'/s','My','/fd','SHA256')
 if($TimestampUri) { $arguments+=@('/tr',$TimestampUri,'/td','SHA256') }
 Invoke-ProcSightTool $signTool ($arguments+@($artifact))
 # A locally self-signed development certificate need not be trusted on this build machine.
 $signature=Get-AuthenticodeSignature -LiteralPath $artifact
 if($signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) { throw 'Signed artifact does not match the selected certificate.' }
 if($inputs.Production) { Invoke-ProcSightTool $signTool @('verify','/pa','/v',$artifact) }
}
if($FeedUri) {
 New-ProcSightAppInstaller $inputs $FeedUri $PackageUri (Join-Path $buildRoot "ProcSight-$($inputs.ProductVersion).appinstaller")
}
$hash=(Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
[pscustomobject]@{ Version=$inputs.ProductVersion;PackageVersion=$inputs.InstallerVersion;Commit=$commit;Profile=$Profile;Artifact=[IO.Path]::GetFileName($artifact);SHA256=$hash;Signed=[bool]$certificate } |
 ConvertTo-Json | Set-Content -LiteralPath (Join-Path $buildRoot 'release-artifacts.json') -Encoding utf8NoBOM
Write-Output "Package inspected: $artifact"
if(!$certificate -and $Profile -eq 'Direct') { Write-Output 'UNSIGNED DEVELOPMENT OUTPUT: not installable until explicitly signed with a trusted certificate.' }
if(!$certificate -and $Profile -eq 'Msi') { Write-Output 'UNSIGNED MSI: no certificate import is required; Windows reputation or managed-device policy may warn or block installation.' }
if($Profile -eq 'Store') { Write-Output 'STORE PREPARATION ONLY: certification, elevation approval and publication are not performed.' }
# Return a structured result for local release orchestration; tool output is routed to the host.
[pscustomobject]@{ Artifact=$artifact;Manifest=(Join-Path $buildRoot 'release-artifacts.json');Version=$inputs.ProductVersion;Commit=$commit }
