#requires -Version 7.4
# Product adapter only. The exact shared checkout owns packaging, signing, SDK
# discovery, diagnostics and final hash/signature measurement. Product identities,
# payload approval and public release compatibility belong to this repository.
function Get-ProcSightBuildToolsRoot([string]$Root) {
 $tools=Join-Path $Root 'external/winbuildtools'
 foreach($relative in @('VERSION','msi/package.ps1','msi/config-v2.schema.json','msi/Scope.ps1','msi/Semantics.ps1','msix/inspect.ps1','common/Tools.ps1','common/Artifacts.ps1','signing/Signing.ps1','signing/Certificates.ps1','signing/new-dev-certificate.ps1')) {
  if(!(Test-Path -LiteralPath (Join-Path $tools $relative) -PathType Leaf)) {
   throw 'winbuildtools checkout is missing or incomplete. Run: git submodule update --init --recursive'
  }
 }
 return $tools
}
$script:ProcSightBuildToolsRoot=Get-ProcSightBuildToolsRoot (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
. (Join-Path $script:ProcSightBuildToolsRoot 'common/Tools.ps1')
. (Join-Path $script:ProcSightBuildToolsRoot 'common/Artifacts.ps1')
. (Join-Path $script:ProcSightBuildToolsRoot 'signing/Signing.ps1')

function Get-ProcSightSourceState([string]$Root) {
 $commit=(& git -C $Root rev-parse HEAD).Trim()
 if($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
 # User/global submodule-ignore preferences must not hide altered executable tools.
 $state=@(& git -C $Root status --porcelain --untracked-files=normal --ignore-submodules=none)
 if($LASTEXITCODE -ne 0) { throw 'Cannot determine source cleanliness.' }
 [pscustomobject]@{Commit=$commit;Dirty=$state.Count -gt 0}
}

function Assert-ProcSightPayload([string]$Folder,[string[]]$RequiredFiles) {
 $files=@(Get-WbtPayloadFiles $Folder)
 $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($file in $files) {
  $relative=$file.Relative.Replace('\','/')
  if(!(Test-ProcSightPackageFile $relative)) { throw "Forbidden package content: $relative" }
  if(!$names.Add($relative)) { throw 'Duplicate package path.' }
 }
 foreach($required in $RequiredFiles) {
  if(!$names.Contains($required)) { throw "Package missing required file: $required" }
 }
 return $files
}

function New-ProcSightMsiConfiguration {
 param([string]$Root,[string]$Folder,[string]$Version,[string]$Path)
 Assert-ProcSightMsiVersion $Version
 if(Test-Path -LiteralPath $Path) { throw 'Resolved MSI configuration already exists.' }
 $config=Get-Content -LiteralPath (Join-Path $Root 'packaging/msi.json') -Raw | ConvertFrom-Json -AsHashtable
 $files=@(Assert-ProcSightPayload $Folder $config.payload.requiredFiles)
 $namespace=$config.identityNamespace
 # The original installer identity is a product compatibility contract. Supply
 # resolved GUIDs to shared tools, which must not recognize ProcSight's scheme.
 function ProductGuid([string]$Scope) {
  $bytes=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($namespace+'/'+$Scope.ToLowerInvariant()))
  ([guid]::new([byte[]]$bytes[0..15])).ToString('B').ToUpperInvariant()
 }
 $config.Remove('identityNamespace') | Out-Null
 $config.product.productCode=ProductGuid "product/x64/$Version"
 # Shared authoring receives the exact already approved file set, including
 # extensionless runtime license/notice files. No extension-wide permission is
 # needed once this concrete payload has passed the product's privacy policy.
 $config.payload.allowedExtensions=@()
 $config.payload.allowedFiles=@($files | ForEach-Object { $_.Relative.Replace('\','/') })
 $config.components=@{}
 foreach($file in $files) { $config.components[$file.Relative.Replace('\','/')]=ProductGuid ('component/'+$file.Relative.Replace('\','/')) }
 if(Test-Path -LiteralPath (Join-Path $Folder 'Assets/ProcSight.ico') -PathType Leaf) { $config.application.icon='Assets/ProcSight.ico' }
 Assert-WbtNoLinks $Path
 $stream=[IO.File]::Open($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try { $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($config | ConvertTo-Json -Depth 12)); $stream.Write($bytes) } finally { $stream.Dispose() }
}

function New-ProcSightInstaller {
 param([string]$Root,[string]$Folder,[string]$Version,[string]$BuildRoot)
 $tools=Get-ProcSightBuildToolsRoot $Root
 $config=Join-Path $BuildRoot 'resolved-msi.json'
 New-ProcSightMsiConfiguration $Root $Folder $Version $config
 & (Join-Path $tools 'msi/package.ps1') -Config $config -PayloadDirectory $Folder -Version $Version -Architecture x64 -OutputDirectory (Join-Path $BuildRoot 'installer')
}

function New-ProcSightMsixExpectation {
 param([string]$Map,[string]$BuildRoot)
 # The WinUI SDK owns resource generation and its map defines the approved build
 # inputs/destinations. Stage from those inputs, never from unpacked package bytes.
 $payload=Join-Path $BuildRoot 'expected-msix'; $manifest=Join-Path $BuildRoot 'expected-AppxManifest.xml'
 Assert-WbtNoLinks $payload; Assert-WbtNoLinks $manifest; Assert-WbtNoLinks $Map
 if((Test-Path $payload) -or (Test-Path $manifest)) { throw 'MSIX expectation output already exists.' }
 $entries=[Collections.Generic.List[object]]::new()
 $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($line in Get-Content -LiteralPath $Map) {
  if([string]::IsNullOrWhiteSpace($line) -or $line -ceq '[Files]') { continue }
  if($line -notmatch '^"(?<source>[^"]+)"\s+"(?<relative>[^"]+)"$') { throw 'Invalid SDK package map entry.' }
  $source=$Matches.source; $relative=$Matches.relative.Replace('\','/')
  if([IO.Path]::IsPathRooted($relative) -or $relative -match '[\x00-\x1f:]' -or @($relative.Split('/') | Where-Object { $_ -in @('','..','.') }).Count -or !$names.Add($relative)) { throw 'Invalid or duplicate SDK package map destination.' }
  if($relative -cne 'AppxManifest.xml' -and !(Test-ProcSightPackageFile $relative)) { throw "Forbidden SDK package map content: $relative" }
  Assert-WbtNoLinks $source
  if(!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'SDK package map input is missing.' }
  $entries.Add([pscustomobject]@{Source=$source;Relative=$relative})
  if($entries.Count -gt 10000) { throw 'SDK package map exceeds payload bounds.' }
 }
 if(!$names.Contains('AppxManifest.xml')) { throw 'SDK package map is missing its manifest.' }
 $null=New-Item -ItemType Directory -Path $payload
 foreach($entry in $entries) {
  $target=if($entry.Relative -ceq 'AppxManifest.xml') { $manifest } else { Join-Path $payload $entry.Relative }
  $null=New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
  Copy-Item -LiteralPath $entry.Source -Destination $target
 }
 [pscustomobject]@{Payload=$payload;Manifest=$manifest}
}

function Complete-ProcSightArtifact {
 param([string]$Root,[string]$Artifact,[string]$BuildRoot,$Inputs,[string]$Commit,[switch]$SourceDirty,[string]$CertificateThumbprint,[string]$TimestampUri)
 $tools=Get-ProcSightBuildToolsRoot $Root
 if($CertificateThumbprint) {
  $null=& (Join-Path $tools 'signing/sign.ps1') -Artifact $Artifact -CertificateThumbprint $CertificateThumbprint -TimestampUri $TimestampUri -RequireTrusted:$Inputs.Production
 }
 $metadata=Write-WbtArtifactManifest -Artifact $Artifact -OutputPath (Join-Path $BuildRoot 'artifact-metadata.json') -Version $Inputs.ProductVersion -SourceCommit $Commit -SourceDirty:$SourceDirty
 $record=Get-Content -LiteralPath $metadata.Manifest -Raw | ConvertFrom-Json -AsHashtable
 $record.Add('PackageVersion',$Inputs.InstallerVersion)
 $record.Add('Profile',$Inputs.Profile)
 $record.Add('Commit',$record.SourceCommit)
 $manifest=Join-Path $BuildRoot 'release-artifacts.json'; Assert-WbtNoLinks $manifest
 $stream=[IO.File]::Open($manifest,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try { $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($record | ConvertTo-Json -Depth 10)); $stream.Write($bytes) } finally { $stream.Dispose() }
 [pscustomobject]@{ Artifact=$metadata.Artifact;Manifest=$manifest;Version=$Inputs.ProductVersion;Commit=$record.SourceCommit }
}
