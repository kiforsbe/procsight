#requires -Version 7.4
. (Join-Path $PSScriptRoot '../release/Common.ps1')
. (Join-Path $PSScriptRoot 'ProcSight.ps1')
function Get-ProcSightPackageInputs {
 param([string]$Root,[ValidateSet('Direct','Store','Msi')][string]$Profile,[string]$IdentityFile,[string]$ElevationMode,[string]$VersionForValidation)
 if($ElevationMode -and $ElevationMode -notin @('Disabled','Approved')) { throw 'Invalid elevation mode.' }
 if($Profile -eq 'Msi' -and $ElevationMode) { throw 'ElevationMode is an MSIX capability option; MSI keeps normal startup and explicit administrator restart.' }
 Assert-ProcSightVersionConsistency $Root
 $version=Read-ProcSightVersion $Root
 if($VersionForValidation) { $version=(Get-ProcSightReleaseVersion -Current $version -Version $VersionForValidation).Version }
 $null=Get-ProcSightReleaseVersion -Current $version -Version $version -Store:($Profile -eq 'Store')
 if($Profile -eq 'Msi') { Assert-ProcSightMsiVersion $version }
 $identity=[pscustomobject]@{ Name='ProcSight.Development'; Publisher='CN=ProcSight Development'; PublisherDisplayName='ProcSight development' }
 if($Profile -eq 'Store' -and (!$IdentityFile -or !$ElevationMode)) { throw 'Store requires reserved IdentityFile and explicit ElevationMode Disabled or Approved.' }
 if($IdentityFile) {
  $identity=Get-Content -LiteralPath $IdentityFile -Raw | ConvertFrom-Json
  if($identity.Name -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$' -or $identity.Name -match 'Development|REPLACE|EXAMPLE' -or
   !$identity.Publisher.StartsWith('CN=') -or $identity.Publisher -match 'Development|REPLACE|EXAMPLE' -or [string]::IsNullOrWhiteSpace($identity.PublisherDisplayName)) { throw 'Provide a complete production package identity; example/development placeholders are not accepted.' }
  $null=[Security.Cryptography.X509Certificates.X500DistinguishedName]::new([string]$identity.Publisher)
 }
 [pscustomobject]@{ Name=$identity.Name; Publisher=$identity.Publisher; PublisherDisplayName=$identity.PublisherDisplayName;
  Version="$version.0"; InstallerVersion=if($Profile -eq 'Msi'){$version}else{"$version.0"}; ProductVersion=$version; Production=[bool]$IdentityFile; AllowsElevation=($Profile -ne 'Store' -or $ElevationMode -eq 'Approved'); Profile=$Profile }
}
function Get-ProcSightSdkTool([string]$Name) {
 Get-WbtSdkTool $Name
}
function Invoke-ProcSightTool([string]$Tool,[string[]]$Arguments) {
 Invoke-WbtTool -Tool $Tool -Arguments $Arguments -LogPath $script:ProcSightToolLog
}
function Test-ProcSightPackageFile([string]$Name) {
 $name=$Name.Replace('\','/')
 if($name.StartsWith('/') -or $name.Split('/') -contains '..' -or $name -match '(^|/)(\.git|src|tests|local-config|local-data)(/|$)') { return $false }
 if($name -match '(?i)(settings|credentials|secrets|knowledge)\.json$|\.(pfx|p12|key|pem|cer|db|sqlite|log|etl|dmp|pdb|cs|ps1|csproj)$') { return $false }
 if($name.StartsWith('Licenses/')) { return [IO.Path]::GetExtension($name) -in @('.txt','.html','.md','') }
 # Windows App SDK's self-contained WinUI map-control asset, not application web content.
 if($name -ceq 'Microsoft.UI.Xaml/Assets/map.html') { return $true }
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
 Assert-WbtSigningMetadata $Certificate $Publisher
}
function Get-ProcSightSigningCertificate([string]$Thumbprint,[string]$Publisher,[switch]$Production) {
 $cert=Get-WbtSigningCertificate -CertificateThumbprint $Thumbprint -Publisher $Publisher
 try {
 if($Production) {
  $chain=[Security.Cryptography.X509Certificates.X509Chain]::new()
  try { $chain.ChainPolicy.RevocationMode='Online'; if(!$chain.Build($cert) -or $chain.ChainElements.Count -lt 2) { throw 'Production signing requires a trusted CA-issued code-signing certificate.' } } finally { $chain.Dispose() }
 }
 return $cert
 } catch { $cert.Dispose(); throw }
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
