#requires -Version 7.0

function Invoke-ProcSightDependencyDotnet {
 param([string]$Root,[string[]]$Arguments)
 Push-Location $Root
 try {
  $output=@(& dotnet @Arguments 2>&1)
  if($LASTEXITCODE -ne 0) { throw "Dependency command failed: dotnet $($Arguments -join ' '). Re-run that command for diagnostics; dependency changes will be restored." }
  return ($output -join [Environment]::NewLine)
 } finally { Pop-Location }
}

function Get-ProcSightDependencyReport {
 param([string]$Root,[ValidateSet('outdated','vulnerable','deprecated')][string]$Kind)
 $json=Invoke-ProcSightDependencyDotnet $Root @('list','ProcSight.slnx','package',"--$Kind",'--include-transitive','--format','json')
 return ($json | ConvertFrom-Json -AsHashtable)
}

function Get-ProcSightDependencyRows {
 param([System.Collections.IDictionary]$Report)
 foreach($project in $Report.projects) {
  foreach($framework in @($project['frameworks'])) {
   if(!$framework) { continue }
   foreach($key in @('topLevelPackages','transitivePackages')) {
    foreach($package in @($framework[$key])) { if($package) { $package } }
   }
  }
 }
}

function Assert-ProcSightVulnerabilityReport {
 param([System.Collections.IDictionary]$Report)
 if(!$Report.Contains('version') -or $Report.version -ne 1 -or !$Report.Contains('projects') -or !@($Report.projects).Count) {
  throw 'NuGet vulnerability report is incomplete or unsupported.'
 }
 $rows=@(Get-ProcSightDependencyRows $Report)
 if($rows.Count) { throw 'Known-vulnerable dependencies were reported. Update the dependency graph before packaging.' }
}

function Get-ProcSightPackageAuditArguments {
 # Treat unavailable advisory data and all severity levels as errors on the profile's own restore.
 return @('--force','-p:NuGetAudit=true','-p:NuGetAuditMode=all','-p:NuGetAuditLevel=low',
  '-p:WarningsAsErrors=NU1900%3BNU1901%3BNU1902%3BNU1903%3BNU1904%3BNU1905','-p:RestoreIgnoreFailedSources=false')
}

function Get-ProcSightDependencyMetadata {
 param([string]$Id,[string]$Version)
 if($Id -notmatch '^[a-zA-Z0-9_.-]+$' -or $Version -notmatch '^\d+(\.\d+){1,3}([+-][a-zA-Z0-9.-]+)?$') { throw 'Invalid NuGet identity.' }
 $uri='https://api.nuget.org/v3/registration5-gz-semver2/'+$Id.ToLowerInvariant()+'/'+$Version.ToLowerInvariant()+'.json'
 $record=Invoke-RestMethod -Uri $uri -TimeoutSec 30
 $catalog=$record.catalogEntry
 if($catalog -is [string]) {
  $catalogUri=[uri]$catalog
  if($catalogUri.Scheme -ne 'https' -or $catalogUri.Host -ne 'api.nuget.org') { throw 'Unexpected NuGet catalog origin.' }
  $catalog=Invoke-RestMethod -Uri $catalogUri -TimeoutSec 30
 }
 return $catalog
}

function Assert-ProcSightDependencyMetadata {
 param([string]$Id,[string]$Version,$Metadata)
 if($Version.Contains('-')) { throw "$Id $Version is a prerelease." }
 $properties=$Metadata.PSObject.Properties.Name
 if('listed' -notin $properties -or !$Metadata.listed) { throw "$Id $Version is unlisted or listing status is unavailable." }
 if('deprecation' -in $properties -and $Metadata.deprecation) { throw "$Id $Version is deprecated; review its supported replacement." }
 if('vulnerabilities' -in $properties -and @($Metadata.vulnerabilities).Count) { throw "$Id $Version has reported vulnerabilities." }
}

function Get-ProcSightDependencySelection {
 param([string]$Root)
 $pairs=@{}
 foreach($base in @('src','tests')) {
  foreach($file in Get-ChildItem -LiteralPath (Join-Path $Root $base) -Filter packages.lock.json -Recurse -File) {
   if($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
   $lock=Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -AsHashtable
   foreach($framework in $lock.dependencies.Values) {
    foreach($id in $framework.Keys) {
     $entry=$framework[$id]
     if($entry.type -eq 'Project') { continue }
     $pairs[$id+'/'+$entry.resolved]=@{Id=$id;Version=$entry.resolved}
    }
   }
  }
 }
 return @($pairs.Values | Sort-Object Id,Version)
}

function Set-ProcSightDependencyVersions {
 param([string]$Path,[object[]]$Updates)
 $document=[xml]::new(); $document.PreserveWhitespace=$true; $document.Load($Path)
 foreach($update in $Updates) {
  $nodes=@($document.SelectNodes('/Project/ItemGroup/PackageVersion') | Where-Object { $_.Include -eq $update.Id })
  if($nodes.Count -ne 1) { throw "Dependency $($update.Id) must have one approved central version." }
  $nodes[0].SetAttribute('Version',$update.Version)
 }
 $document.Save($Path)
}

function Invoke-ProcSightDependencyTransaction {
 param([string[]]$Paths,[scriptblock]$Action)
 $backups=@{}
 foreach($path in $Paths) { $backups[$path]=[IO.File]::ReadAllBytes($path) }
 $completed=$false
 try { & $Action; $completed=$true }
 finally {
  if(!$completed) { foreach($path in $Paths) { [IO.File]::WriteAllBytes($path,$backups[$path]) } }
 }
}
