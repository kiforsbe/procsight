# Product version inventory only. Dependency versions and historical examples are deliberately excluded.
function Get-ProcSightReleaseVersion {
 param([Parameter(Mandatory)][string]$Current,[string]$Version,[ValidateSet('Major','Minor','Patch')][string]$Bump,[switch]$Store)
 function Parse-Version([string]$value) {
  if($value -notmatch '^(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})$') { throw 'Use stable SemVer MAJOR.MINOR.PATCH without suffixes or leading zeroes.' }
  $parts=@($value.Split('.') | ForEach-Object { [int]$_ })
  # AssemblyVersion's usable range is narrower than MSIX's UInt16 range.
  if(@($parts | Where-Object { $_ -gt 65534 }).Count) { throw 'Version components must be between 0 and 65534 (assembly and MSIX compatible).' }
  return ,$parts
 }
 $parts=Parse-Version $Current
 if([bool]$Version -eq [bool]$Bump) { throw 'Choose exactly one explicit Version or Bump.' }
 if($Bump) {
  switch($Bump) { Major { $parts[0]++; $parts[1]=0; $parts[2]=0 }; Minor { $parts[1]++; $parts[2]=0 }; Patch { $parts[2]++ } }
  $Version=$parts -join '.'
 }
 $parts=Parse-Version $Version
 if($Store -and $parts[0] -eq 0) { throw 'Store preparation requires a major version of at least 1.' }
 [pscustomobject]@{ Version=$Version; WindowsVersion="$Version.0"; Tag="v$Version" }
}
function Read-ProcSightVersion([string]$Root) {
 [xml]$document=Get-Content -LiteralPath (Join-Path $Root 'Version.props') -Raw
 $value=[string]$document.Project.PropertyGroup.VersionPrefix
 (Get-ProcSightReleaseVersion -Current $value -Version $value).Version
}
function Get-ProcSightVersionTargets([string]$Root) {
 @('Version.props','src/ProcSight.App/app.manifest','src/ProcSight.App/Package.appxmanifest') |
  Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) }
}
function Set-ProcSightProductVersion([string]$Root,[string]$Version) {
 $value=Get-ProcSightReleaseVersion -Current (Read-ProcSightVersion $Root) -Version $Version
 foreach($relative in Get-ProcSightVersionTargets $Root) {
  $path=Join-Path $Root $relative
  $document=[xml]::new(); $document.PreserveWhitespace=$true; $document.Load($path)
  switch($relative) {
   'Version.props' { $document.SelectSingleNode("//*[local-name()='VersionPrefix']").InnerText=$value.Version }
   'src/ProcSight.App/app.manifest' { $document.SelectSingleNode("/*[local-name()='assembly']/*[local-name()='assemblyIdentity']").SetAttribute('version',$value.WindowsVersion) }
   'src/ProcSight.App/Package.appxmanifest' { $document.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']").SetAttribute('Version',$value.WindowsVersion) }
  }
  $document.Save($path)
 }
}
function Assert-ProcSightVersionConsistency([string]$Root) {
 $value=Read-ProcSightVersion $Root
 foreach($relative in Get-ProcSightVersionTargets $Root) {
  [xml]$document=Get-Content -LiteralPath (Join-Path $Root $relative) -Raw
  $actual=switch($relative) {
   'Version.props' { "$value.0" }
   'src/ProcSight.App/app.manifest' { $document.SelectSingleNode("/*[local-name()='assembly']/*[local-name()='assemblyIdentity']").GetAttribute('version') }
   'src/ProcSight.App/Package.appxmanifest' { $document.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']").GetAttribute('Version') }
  }
  if($actual -cne "$value.0") { throw "Product version drift in $relative. Use prepare-release.ps1 to synchronize versions." }
 }
}
function New-ProcSightReleaseChangelog([string]$Text,[string]$Version,[string]$ReleaseDate) {
 if($Text -match "(?m)^## \[$([regex]::Escape($Version))\]") { throw 'Changelog already contains this release.' }
 $match=[regex]::Match($Text,'(?ms)^## \[Unreleased\][^\S\r\n]*\r?\n(?<body>.*?)(?=^## \[|\z)')
 if(!$match.Success -or ($match.Groups['body'].Value -replace '(?m)^#+.*$','').Trim().Length -eq 0) { throw 'Changelog must contain populated Unreleased notes.' }
 $section="## [Unreleased]`n`n## [$Version] - $ReleaseDate`n`n"+$match.Groups['body'].Value.Trim()+"`n`n"
 $Text.Substring(0,$match.Index)+$section+$Text.Substring($match.Index+$match.Length)
}
