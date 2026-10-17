. (Join-Path $PSScriptRoot '../release/Common.ps1')
function Get-ProcSightPackageInputs {
 param([string]$Root,[ValidateSet('Direct','Store')][string]$Profile,[string]$IdentityFile,[string]$ElevationMode,[string]$VersionForValidation)
 if($ElevationMode -and $ElevationMode -notin @('Disabled','Approved')) { throw 'Invalid elevation mode.' }
 Assert-ProcSightVersionConsistency $Root
 $version=Read-ProcSightVersion $Root
 if($VersionForValidation) { $version=(Get-ProcSightReleaseVersion -Current $version -Version $VersionForValidation).Version }
 $null=Get-ProcSightReleaseVersion -Current $version -Version $version -Store:($Profile -eq 'Store')
 $identity=[pscustomobject]@{ Name='ProcSight.Development'; Publisher='CN=ProcSight Development'; PublisherDisplayName='ProcSight development' }
 if($Profile -eq 'Store' -and (!$IdentityFile -or !$ElevationMode)) { throw 'Store requires reserved IdentityFile and explicit ElevationMode Disabled or Approved.' }
 if($IdentityFile) {
  $identity=Get-Content -LiteralPath $IdentityFile -Raw | ConvertFrom-Json
  if($identity.Name -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$' -or $identity.Name -match 'Development|REPLACE|EXAMPLE' -or
   !$identity.Publisher.StartsWith('CN=') -or $identity.Publisher -match 'Development|REPLACE|EXAMPLE' -or [string]::IsNullOrWhiteSpace($identity.PublisherDisplayName)) { throw 'Provide a complete production package identity; example/development placeholders are not accepted.' }
  $null=[Security.Cryptography.X509Certificates.X500DistinguishedName]::new([string]$identity.Publisher)
 }
 [pscustomobject]@{ Name=$identity.Name; Publisher=$identity.Publisher; PublisherDisplayName=$identity.PublisherDisplayName;
  Version="$version.0"; ProductVersion=$version; Production=[bool]$IdentityFile; AllowsElevation=($Profile -eq 'Direct' -or $ElevationMode -eq 'Approved'); Profile=$Profile }
}
function Get-ProcSightSdkTool([string]$Name) {
 if($Name -notin @('makeappx.exe','signtool.exe')) { throw 'Unknown SDK tool.' }
 $sdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
 $version=Get-ChildItem -LiteralPath $sdkRoot -Directory | Where-Object { $_.Name -match '^10\.0\.\d+\.0$' -and [version]$_.Name -ge [version]'10.0.26100.0' } | Sort-Object { [version]$_.Name } -Descending
 foreach($candidate in $version) { $path=Join-Path $candidate.FullName "x64/$Name"; if(Test-Path -LiteralPath $path) { return $path } }
 throw "Install Windows SDK 10.0.26100.0 or later with $Name (x64)."
}
function Invoke-ProcSightTool([string]$Tool,[string[]]$Arguments) {
 $messages=@(& $Tool @Arguments 2>&1); $result=$LASTEXITCODE
 if($script:ProcSightToolLog) { $messages | Out-String | Add-Content -LiteralPath $script:ProcSightToolLog }
 if($result -ne 0) { $messages | Select-Object -Last 12 | Out-Host; throw "$([IO.Path]::GetFileName($Tool)) failed (exit $result)." }
 Write-Host "$([IO.Path]::GetFileName($Tool)): succeeded"
}
function Test-ProcSightPackageFile([string]$Name) {
 $name=$Name.Replace('\','/')
 if($name.StartsWith('/') -or $name.Split('/') -contains '..' -or $name -match '(^|/)(\.git|src|tests|local-config|local-data)(/|$)') { return $false }
 if($name -match '(?i)(settings|credentials|secrets|knowledge)\.json$|\.(pfx|p12|key|pem|cer|db|sqlite|log|etl|dmp|pdb|cs|ps1|csproj)$') { return $false }
 if($name.StartsWith('Licenses/')) { return [IO.Path]::GetExtension($name) -in @('.txt','.html','.md','') }
 if([IO.Path]::GetFileName($name) -in @('AppxManifest.xml','AppxBlockMap.xml','[Content_Types].xml','AppxSignature.p7x')) { return $true }
 if($name.EndsWith('.json')) { return $name -in @('ProcSight.App.deps.json','ProcSight.App.runtimeconfig.json') }
 return [IO.Path]::GetExtension($name) -in @('.dll','.exe','.winmd','.pri','.xbf','.mui','.png','.ico','.svg','.txt','.manifest')
}
function Assert-ProcSightPackageContents([string]$Folder,$Inputs) {
 [xml]$manifest=Get-Content -LiteralPath (Join-Path $Folder 'AppxManifest.xml') -Raw
 $identity=$manifest.Package.Identity
 if($identity.Name -cne $Inputs.Name -or $identity.Publisher -cne $Inputs.Publisher -or $identity.Version -cne $Inputs.Version -or $identity.ProcessorArchitecture -ne 'x64') { throw 'Generated package identity/version/architecture mismatch.' }
 $capabilities=@($manifest.Package.Capabilities.ChildNodes | Where-Object { $_.NodeType -eq 'Element' } | ForEach-Object { $_.GetAttribute('Name') })
 if('runFullTrust' -notin $capabilities -or (@($capabilities | Where-Object { $_ -notin @('runFullTrust','allowElevation') }).Count) -or
  (('allowElevation' -in $capabilities) -ne $Inputs.AllowsElevation)) { throw 'Unexpected package privilege declarations.' }
 if($manifest.Package.Dependencies.TargetDeviceFamily.MinVersion -ne '10.0.26100.0') { throw 'Unexpected minimum Windows version.' }
 foreach($required in @('ProcSight.App.exe','ProcSight.App.dll','ProcSight-LICENSE.txt','ProcSight-THIRD-PARTY-NOTICES.txt','Assets/Package/StoreLogo.png')) {
  if(!(Test-Path -LiteralPath (Join-Path $Folder $required))) { throw "Package missing required file: $required" }
 }
 foreach($file in Get-ChildItem -LiteralPath $Folder -File -Recurse) {
  $relative=[IO.Path]::GetRelativePath($Folder,$file.FullName)
  if(!(Test-ProcSightPackageFile $relative)) { throw "Forbidden package content: $relative" }
 }
}
function Assert-ProcSightFeedUri([string]$Value) {
 $uri=$null
 if(![uri]::TryCreate($Value,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment) { throw 'Update locations must be absolute HTTPS URLs without userinfo or fragments.' }
}
function Assert-ProcSightSigningMetadata($Certificate,[string]$Publisher) {
 if($Certificate.Subject -cne $Publisher -or !$Certificate.HasPrivateKey -or $Certificate.NotBefore -gt (Get-Date) -or $Certificate.NotAfter -le (Get-Date) -or
  '1.3.6.1.5.5.7.3.3' -notin $Certificate.UsageOids) { throw 'Select an unexpired code-signing certificate with private key and matching publisher.' }
}
function Get-ProcSightSigningCertificate([string]$Thumbprint,[string]$Publisher,[switch]$Production) {
 if($Thumbprint -notmatch '^[a-fA-F0-9]{40}$') { throw 'CertificateThumbprint must identify an explicit certificate in CurrentUser\My.' }
 $cert=Get-Item -LiteralPath "Cert:\CurrentUser\My\$Thumbprint"
 $oids=@($cert.Extensions | Where-Object { $_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] } | ForEach-Object { $_.EnhancedKeyUsages } | ForEach-Object { $_.Value })
 Assert-ProcSightSigningMetadata ([pscustomobject]@{Subject=$cert.Subject; HasPrivateKey=$cert.HasPrivateKey; NotBefore=$cert.NotBefore; NotAfter=$cert.NotAfter; UsageOids=$oids}) $Publisher
 if($Production) {
  $chain=[Security.Cryptography.X509Certificates.X509Chain]::new()
  try { $chain.ChainPolicy.RevocationMode='Online'; if(!$chain.Build($cert) -or $chain.ChainElements.Count -lt 2) { throw 'Production signing requires a trusted CA-issued code-signing certificate.' } } finally { $chain.Dispose() }
 }
 return $cert
}
function New-ProcSightAppInstaller($Inputs,[string]$FeedUri,[string]$PackageUri,[string]$Path) {
 Assert-ProcSightFeedUri $FeedUri; Assert-ProcSightFeedUri $PackageUri
 $feed=[xml]::new(); $null=$feed.AppendChild($feed.CreateXmlDeclaration('1.0','utf-8',$null))
 $ns='http://schemas.microsoft.com/appx/appinstaller/2018'; $element=$feed.CreateElement('AppInstaller',$ns); $null=$feed.AppendChild($element)
 $element.SetAttribute('Uri',$FeedUri); $element.SetAttribute('Version',$Inputs.Version)
 $package=$feed.CreateElement('MainPackage',$ns); $null=$element.AppendChild($package)
 foreach($pair in @{Name=$Inputs.Name;Publisher=$Inputs.Publisher;Version=$Inputs.Version;ProcessorArchitecture='x64';Uri=$PackageUri}.GetEnumerator()) { $package.SetAttribute($pair.Key,$pair.Value) }
 $updates=$feed.CreateElement('UpdateSettings',$ns); $null=$element.AppendChild($updates)
 $launch=$feed.CreateElement('OnLaunch',$ns); $launch.SetAttribute('HoursBetweenUpdateChecks','24'); $launch.SetAttribute('ShowPrompt','false'); $launch.SetAttribute('UpdateBlocksActivation','false'); $null=$updates.AppendChild($launch)
 $feed.Save($Path)
}
