#requires -Version 7.0
# Native Windows Installer authoring only; no executable custom actions or product installation.
function Get-ProcSightMsiGuid([string]$Scope) {
 $bytes=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('ProcSight.Msi/'+$Scope.ToLowerInvariant()))
 return ([guid]::new([byte[]]$bytes[0..15])).ToString('B').ToUpperInvariant()
}
function Get-ProcSightMsiId([string]$Prefix,[string]$Scope) {
 return $Prefix+(Get-ProcSightMsiGuid $Scope).Replace('-','').Trim('{}')
}
function Get-ProcSightMsiPayload([string]$Folder) {
 foreach($required in @('ProcSight.App.exe','ProcSight.App.dll','ProcSight-LICENSE.txt','ProcSight-THIRD-PARTY-NOTICES.txt','Assets/Package/StoreLogo.png')) {
  if(!(Test-Path -LiteralPath (Join-Path $Folder $required) -PathType Leaf)) { throw "MSI payload missing $required." }
 }
 foreach($item in Get-ChildItem -LiteralPath $Folder -Recurse -Force) {
  if($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Installer payload must not contain links or reparse points.' }
 }
 $files=@(Get-ChildItem -LiteralPath $Folder -Recurse -File | Sort-Object { [IO.Path]::GetRelativePath($Folder,$_.FullName).ToLowerInvariant() })
 if(!$files.Count -or $files.Count -gt 10000 -or ($files | Measure-Object Length -Sum).Sum -gt 2GB) { throw 'MSI payload exceeds its bounded single-cabinet limits.' }
 foreach($file in $files) {
  $relative=[IO.Path]::GetRelativePath($Folder,$file.FullName).Replace('\','/')
  if(!(Test-ProcSightPackageFile $relative) -or $file.FullName -match '[\r\n"%]') { throw "Forbidden or unsupported MSI payload file: $relative" }
  [pscustomobject]@{Id=(Get-ProcSightMsiId 'F' $relative);Component=(Get-ProcSightMsiId 'C' $relative);Relative=$relative;FullName=$file.FullName;Name=$file.Name;Length=$file.Length}
 }
}
function Close-ProcSightMsiCom($Object) {
 if($null -ne $Object -and [Runtime.InteropServices.Marshal]::IsComObject($Object)) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($Object) }
}
function Invoke-ProcSightMsiSql($Database,[string]$Sql) {
 $view=$null
 try { $view=$Database.OpenView($Sql); $null=$view.Execute() } finally { if($view) { $null=$view.Close(); Close-ProcSightMsiCom $view } }
}
function Add-ProcSightMsiRow($Installer,$Database,[string]$Table,[string[]]$Columns,[object[]]$Values,[int[]]$Streams=@()) {
 if($Table -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' -or @($Columns | Where-Object { $_ -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' }).Count -or $Columns.Count -ne $Values.Count) { throw "Invalid MSI authoring row: $Table ($($Columns.Count) columns, $($Values.Count) values)." }
 $names=($Columns | ForEach-Object { '`'+$_+'`' }) -join ','
 $parameters=(@('?')*$Values.Count) -join ','
 $view=$null; $record=$null
 try {
  $view=$Database.OpenView(('INSERT INTO `{0}` ({1}) VALUES ({2})' -f $Table,$names,$parameters))
  $record=$Installer.CreateRecord($Values.Count)
  for($i=0;$i -lt $Values.Count;$i++) {
   if($null -eq $Values[$i]) { continue }
   if($i -in $Streams) { $null=$record.SetStream($i+1,[string]$Values[$i]) }
   elseif($Values[$i] -is [int] -or $Values[$i] -is [long]) { $record.IntegerData($i+1)=[int]$Values[$i] }
   else { $record.StringData($i+1)=[string]$Values[$i] }
  }
  $null=$view.Execute($record)
 } finally { Close-ProcSightMsiCom $record; if($view) { $null=$view.Close();Close-ProcSightMsiCom $view } }
}
function Get-ProcSightMsiRows($Database,[string]$Table,[string[]]$Columns) {
 $view=$null
 try {
  $names=($Columns | ForEach-Object { '`'+$_+'`' }) -join ','
  $view=$Database.OpenView(('SELECT {0} FROM `{1}`' -f $names,$Table));$null=$view.Execute()
  $count=0
  while($record=$view.Fetch()) {
   try {
    if(++$count -gt 10000) { throw 'MSI inspection row limit exceeded.' }
    $row=[ordered]@{}
    for($i=0;$i -lt $Columns.Count;$i++) { $row[$Columns[$i]]=$record.StringData($i+1) }
    [pscustomobject]$row
   } finally { Close-ProcSightMsiCom $record }
  }
 } finally { if($view) { $null=$view.Close();Close-ProcSightMsiCom $view } }
}
function New-ProcSightMsi {
 param([string]$Folder,[string]$Path,[string]$Version,[string]$Manufacturer='ProcSight')
 Assert-ProcSightMsiVersion $Version
 if(Test-Path -LiteralPath $Path) { throw 'Refusing to overwrite an existing MSI.' }
 $files=@(Get-ProcSightMsiPayload $Folder)
 $work=Join-Path (Split-Path $Path -Parent) ('msi-authoring-'+[guid]::NewGuid().ToString('N'))
 $null=New-Item -ItemType Directory -Path $work
 $cab=Join-Path $work 'payload.cab';$ddf=Join-Path $work 'payload.ddf'
 $directives=@('.OPTION EXPLICIT','.Set Cabinet=on','.Set Compress=on','.Set CompressionType=LZX','.Set CompressionMemory=21','.Set MaxDiskSize=0','.Set CabinetNameTemplate=payload.cab',('.Set DiskDirectoryTemplate="'+$work+'"'))
 $directives+=@($files | ForEach-Object { '"'+$_.FullName+'" '+$_.Id })
 $directives=@(('.Set InfFileName="'+(Join-Path $work 'setup.inf')+'"'),('.Set RptFileName="'+(Join-Path $work 'setup.rpt')+'"'))+$directives
 [IO.File]::WriteAllLines($ddf,$directives,[Text.Encoding]::UTF8)
 Invoke-ProcSightTool (Join-Path $env:SystemRoot 'System32/makecab.exe') @('/F',$ddf)
 $installer=$null;$database=$null;$summary=$null;$failedPath=$null
 try {
  $installer=New-Object -ComObject WindowsInstaller.Installer
  $database=$installer.OpenDatabase($Path,3)
  $schemas=@{
   Property='`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`'
   Directory='`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`'
   Component='`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`'
   Feature='`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`'
   FeatureComponents='`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`, `Component_`'
   File='`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL PRIMARY KEY `File`'
   Media='`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`'
   Upgrade='`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` LONG NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`, `VersionMin`, `VersionMax`, `Language`, `Attributes`'
   LaunchCondition='`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Condition`'
   AppSearch='`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL PRIMARY KEY `Property`, `Signature_`'
   RegLocator='`Signature_` CHAR(72) NOT NULL, `Root` SHORT, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT PRIMARY KEY `Signature_`'
   Shortcut='`Shortcut` CHAR(72) NOT NULL, `Directory_` CHAR(72) NOT NULL, `Name` CHAR(128) NOT NULL LOCALIZABLE, `Component_` CHAR(72) NOT NULL, `Target` CHAR(255) NOT NULL, `Arguments` CHAR(255), `Description` CHAR(255) LOCALIZABLE, `Hotkey` SHORT, `Icon_` CHAR(72), `IconIndex` SHORT, `ShowCmd` SHORT, `WkDir` CHAR(72) PRIMARY KEY `Shortcut`'
   RemoveFile='`FileKey` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) LOCALIZABLE, `DirProperty` CHAR(72) NOT NULL, `InstallMode` SHORT NOT NULL PRIMARY KEY `FileKey`'
   Icon='`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`'
   MsiFileHash='`File_` CHAR(72) NOT NULL, `Options` SHORT NOT NULL, `HashPart1` LONG NOT NULL, `HashPart2` LONG NOT NULL, `HashPart3` LONG NOT NULL, `HashPart4` LONG NOT NULL PRIMARY KEY `File_`'
   InstallExecuteSequence='`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`'
   InstallUISequence='`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`'
  }
  foreach($table in $schemas.Keys) { Invoke-ProcSightMsiSql $database ('CREATE TABLE `{0}` ({1})' -f $table,$schemas[$table]) }
  $properties=@{ProductName='ProcSight';ProductVersion=$Version;ProductLanguage='1033';Manufacturer=$Manufacturer;ProductCode=(Get-ProcSightMsiGuid "product/x64/$Version");UpgradeCode=(Get-ProcSightMsiGuid 'upgrade/x64');ALLUSERS='1';INSTALLLEVEL='1';ARPNOMODIFY='1';REBOOT='ReallySuppress';MSIRESTARTMANAGERCONTROL='Disable';SecureCustomProperties='OLDERVERSIONS;NEWERVERSIONS;PROCSIGHTWINDOWSBUILD'}
  foreach($name in $properties.Keys) { Add-ProcSightMsiRow $installer $database Property @('Property','Value') @($name,$properties[$name]) }
  foreach($directory in @(@('TARGETDIR',$null,'SourceDir'),@('ProgramFiles64Folder','TARGETDIR','PFiles64'),@('INSTALLFOLDER','ProgramFiles64Folder','ProcSight'),@('ProgramMenuFolder','TARGETDIR','PMenu'),@('PROCSIGHTMENUDIR','ProgramMenuFolder','ProcSight'))) {
   Add-ProcSightMsiRow $installer $database Directory @('Directory','Directory_Parent','DefaultDir') $directory
  }
  $directories=@{''='INSTALLFOLDER'}
  $shortNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  foreach($file in $files) {
   $relativeParent=[IO.Path]::GetDirectoryName($file.Relative).Replace('\','/')
   $parent='INSTALLFOLDER';$scope=''
   foreach($segment in @($relativeParent.Split('/') | Where-Object { $_ })) {
    $scope=if($scope) { $scope+'/'+$segment } else { $segment }
    if(!$directories.ContainsKey($scope)) {
     $id=Get-ProcSightMsiId 'D' $scope
     Add-ProcSightMsiRow $installer $database Directory @('Directory','Directory_Parent','DefaultDir') @($id,$parent,('D'+$id.Substring(1,7)+'|'+$segment))
     if(!$shortNames.Add($parent+'/D'+$id.Substring(1,7))) { throw 'MSI directory short-name collision.' }
     $directories[$scope]=$id
    }
    $parent=$directories[$scope]
   }
   Add-ProcSightMsiRow $installer $database Component @('Component','ComponentId','Directory_','Attributes','KeyPath') @($file.Component,(Get-ProcSightMsiGuid ('component/'+$file.Relative)),$parent,256,$file.Id)
   Add-ProcSightMsiRow $installer $database FeatureComponents @('Feature_','Component_') @('Main',$file.Component)
   $fileVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).FileVersion
   if($fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { $fileVersion=$null }
   $sequence=[array]::IndexOf($files,$file)+1
   $extension=[IO.Path]::GetExtension($file.Name)
   if($extension.Length -gt 4) { $extension='' }
   $short='F'+$file.Id.Substring(1,7)+$extension
   if(!$shortNames.Add($parent+'/'+$short)) { throw 'MSI file short-name collision.' }
   Add-ProcSightMsiRow $installer $database File @('File','Component_','FileName','FileSize','Version','Attributes','Sequence') @($file.Id,$file.Component,($short+'|'+$file.Name),[int]$file.Length,$fileVersion,512,$sequence)
   if(!$fileVersion) {
    $hash=$installer.FileHash($file.FullName,0)
    try { Add-ProcSightMsiRow $installer $database MsiFileHash @('File_','Options','HashPart1','HashPart2','HashPart3','HashPart4') @($file.Id,0,$hash.IntegerData(1),$hash.IntegerData(2),$hash.IntegerData(3),$hash.IntegerData(4)) } finally { Close-ProcSightMsiCom $hash }
   }
  }
  Add-ProcSightMsiRow $installer $database Feature @('Feature','Title','Description','Display','Level','Directory_','Attributes') @('Main','ProcSight','Process and service investigation',1,1,'INSTALLFOLDER',0)
  Add-ProcSightMsiRow $installer $database Media @('DiskId','LastSequence','Cabinet') @(1,$files.Count,'#payload.cab')
  $upgrade=$properties.UpgradeCode
  Add-ProcSightMsiRow $installer $database Upgrade @('UpgradeCode','VersionMax','Attributes','ActionProperty') @($upgrade,$Version,0,'OLDERVERSIONS')
  Add-ProcSightMsiRow $installer $database Upgrade @('UpgradeCode','VersionMin','Attributes','ActionProperty') @($upgrade,$Version,258,'NEWERVERSIONS')
  Add-ProcSightMsiRow $installer $database RegLocator @('Signature_','Root','Key','Name','Type') @('WindowsBuild',2,'SOFTWARE\Microsoft\Windows NT\CurrentVersion','CurrentBuildNumber',18)
  Add-ProcSightMsiRow $installer $database AppSearch @('Property','Signature_') @('PROCSIGHTWINDOWSBUILD','WindowsBuild')
  Add-ProcSightMsiRow $installer $database LaunchCondition @('Condition','Description') @('Installed OR (VersionNT64 AND PROCSIGHTWINDOWSBUILD >= 26100)','ProcSight requires Windows 11 24H2 or later (x64).')
  Add-ProcSightMsiRow $installer $database LaunchCondition @('Condition','Description') @('Installed OR NOT NEWERVERSIONS','The same or a newer version of ProcSight is already installed.')
  $main=@($files | Where-Object Relative -eq 'ProcSight.App.exe')[0]
  $icon=Join-Path $Folder 'Assets/ProcSight.ico'
  $iconName=$null
  if(Test-Path -LiteralPath $icon) {
   $iconName='ProcSight.exe'; Add-ProcSightMsiRow $installer $database Icon @('Name','Data') @($iconName,$icon) @(1)
   Add-ProcSightMsiRow $installer $database Property @('Property','Value') @('ARPPRODUCTICON',$iconName)
  }
  Add-ProcSightMsiRow $installer $database Shortcut @('Shortcut','Directory_','Name','Component_','Target','Description','Icon_','ShowCmd','WkDir') @('StartMenuShortcut','PROCSIGHTMENUDIR','ProcSight',$main.Component,'Main','ProcSight',$iconName,1,'INSTALLFOLDER')
  Add-ProcSightMsiRow $installer $database RemoveFile @('FileKey','Component_','DirProperty','InstallMode') @('RemoveStartMenuFolder',$main.Component,'PROCSIGHTMENUDIR',2)
  $actions=@{FindRelatedProducts=25;AppSearch=50;LaunchConditions=100;ValidateProductID=700;CostInitialize=800;FileCost=900;CostFinalize=1000;MigrateFeatureStates=1200;InstallValidate=1400;InstallInitialize=1500;RemoveExistingProducts=1510;ProcessComponents=1600;UnpublishFeatures=1800;RemoveShortcuts=3200;RemoveFiles=3500;RemoveFolders=3600;CreateFolders=3700;InstallFiles=4000;CreateShortcuts=4500;RegisterUser=6000;RegisterProduct=6100;PublishFeatures=6300;PublishProduct=6400;InstallFinalize=6600}
  foreach($action in $actions.Keys) { Add-ProcSightMsiRow $installer $database InstallExecuteSequence @('Action','Sequence') @($action,[int]$actions[$action]) }
  foreach($action in @('FindRelatedProducts','AppSearch','LaunchConditions','CostInitialize','FileCost','CostFinalize','MigrateFeatureStates')) { Add-ProcSightMsiRow $installer $database InstallUISequence @('Action','Sequence') @($action,[int]$actions[$action]) }
  Add-ProcSightMsiRow $installer $database InstallUISequence @('Action','Sequence') @('ExecuteAction',1300)
  Add-ProcSightMsiRow $installer $database _Streams @('Name','Data') @('payload.cab',$cab) @(1)
  $null=$database.Commit(); Close-ProcSightMsiCom $database; $database=$installer.OpenDatabase($Path,1)
  $summary=$database.SummaryInformation(20)
  $summary.Property(1)=1252; $summary.Property(2)='ProcSight installer'; $summary.Property(3)='ProcSight'; $summary.Property(4)=$Manufacturer
  $summary.Property(7)='x64;1033';$summary.Property(9)=[guid]::NewGuid().ToString('B').ToUpperInvariant();$summary.Property(14)=500;$summary.Property(15)=2;$summary.Property(18)='ProcSight packaging'
  $null=$summary.Persist();$null=$database.Commit()
 } catch { if(Test-Path -LiteralPath $Path) { $failedPath=$Path }; throw }
 finally {
  Close-ProcSightMsiCom $summary;Close-ProcSightMsiCom $database;Close-ProcSightMsiCom $installer
  if($failedPath -and (Test-Path -LiteralPath $failedPath)) { Remove-Item -LiteralPath $failedPath -Force }
 }
}

function Read-ProcSightMsi([string]$Path) {
 $installer=$null;$database=$null;$summary=$null
 try {
  $installer=New-Object -ComObject WindowsInstaller.Installer;$database=$installer.OpenDatabase($Path,0)
  $properties=@{}; foreach($row in Get-ProcSightMsiRows $database Property @('Property','Value')) { $properties[$row.Property]=$row.Value }
  $tables=@(Get-ProcSightMsiRows $database _Tables @('Name') | ForEach-Object Name)
  $summary=$database.SummaryInformation(0)
  [pscustomobject]@{Properties=$properties;Template=$summary.Property(7);Files=@(Get-ProcSightMsiRows $database File @('File','Component_','FileName','FileSize','Sequence'));Components=@(Get-ProcSightMsiRows $database Component @('Component','ComponentId','Directory_','Attributes','KeyPath'));Directories=@(Get-ProcSightMsiRows $database Directory @('Directory','Directory_Parent','DefaultDir'));CustomActionCount=if('CustomAction' -in $tables) {@(Get-ProcSightMsiRows $database CustomAction @('Action')).Count}else{0};RegistryCount=if('Registry' -in $tables){@(Get-ProcSightMsiRows $database Registry @('Registry')).Count}else{0};ShortcutTarget=@(Get-ProcSightMsiRows $database Shortcut @('Target'))[0].Target;UpgradeCount=@(Get-ProcSightMsiRows $database Upgrade @('UpgradeCode')).Count;LaunchConditions=(@(Get-ProcSightMsiRows $database LaunchCondition @('Condition')) | ForEach-Object Condition) -join ';';Actions=@(Get-ProcSightMsiRows $database InstallExecuteSequence @('Action','Sequence'));Media=@(Get-ProcSightMsiRows $database Media @('DiskId','LastSequence','Cabinet'))}
 } finally { Close-ProcSightMsiCom $summary;Close-ProcSightMsiCom $database;Close-ProcSightMsiCom $installer }
}

function Export-ProcSightMsiCabinet([string]$Path,[string]$Destination) {
 if(!('ProcSight.Packaging.MsiStream' -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
namespace ProcSight.Packaging {
 public static class MsiStream {
  [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiOpenDatabase(string path,IntPtr mode,out uint handle);
  [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiDatabaseOpenView(uint db,string query,out uint view);
  [DllImport("msi.dll")] static extern uint MsiViewExecute(uint view,uint record);
  [DllImport("msi.dll")] static extern uint MsiViewFetch(uint view,out uint record);
  [DllImport("msi.dll")] static extern uint MsiRecordReadStream(uint record,uint field,byte[] data,ref uint count);
  [DllImport("msi.dll")] static extern uint MsiCloseHandle(uint handle);
  static void Check(uint result) { if(result!=0) throw new Win32Exception((int)result); }
  public static void Copy(string source,string destination) {
   uint db=0,view=0,record=0;
   try {
    Check(MsiOpenDatabase(source,IntPtr.Zero,out db));
    Check(MsiDatabaseOpenView(db,"SELECT `Data` FROM `_Streams` WHERE `Name`='payload.cab'",out view));
    Check(MsiViewExecute(view,0));Check(MsiViewFetch(view,out record));
    using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write)) {
     var buffer=new byte[65536]; long total=0;
     while(true) { uint count=(uint)buffer.Length;Check(MsiRecordReadStream(record,1,buffer,ref count));if(count==0) break;
      total+=count;if(total>2147483648L) throw new InvalidDataException("MSI cabinet exceeds inspection limit.");output.Write(buffer,0,(int)count); }
    }
   } finally { if(record!=0) MsiCloseHandle(record);if(view!=0) MsiCloseHandle(view);if(db!=0) MsiCloseHandle(db); }
  }
 }
}
'@
 }
 [ProcSight.Packaging.MsiStream]::Copy($Path,$Destination)
}
function Assert-ProcSightMsiContents([string]$Path,[string]$Folder,[string]$Version,[string]$InspectionFolder) {
 $expected=@(Get-ProcSightMsiPayload $Folder);$actual=Read-ProcSightMsi $Path
 if($actual.Properties.ProductVersion -cne $Version -or $actual.Properties.ProductCode -ne (Get-ProcSightMsiGuid "product/x64/$Version") -or $actual.Properties.UpgradeCode -ne (Get-ProcSightMsiGuid 'upgrade/x64') -or $actual.Template -cne 'x64;1033' -or $actual.Properties.ALLUSERS -ne '1') { throw 'MSI identity/version/scope mismatch.' }
 if($actual.CustomActionCount -or $actual.RegistryCount -or $actual.Files.Count -ne $expected.Count -or $actual.Components.Count -ne $expected.Count -or $actual.UpgradeCount -ne 2 -or $actual.ShortcutTarget -cne 'Main') { throw 'MSI contains unexpected actions, files, components or shortcuts.' }
 if($actual.Media.Count -ne 1 -or $actual.Media[0].Cabinet -cne '#payload.cab' -or [int]$actual.Media[0].LastSequence -ne $expected.Count) { throw 'MSI must contain exactly one embedded cabinet.' }
 $null=New-Item -ItemType Directory -Path $InspectionFolder
 $cab=Join-Path $InspectionFolder 'payload.cab';$extracted=Join-Path $InspectionFolder 'files';$null=New-Item -ItemType Directory -Path $extracted
 Export-ProcSightMsiCabinet $Path $cab
 Invoke-ProcSightTool (Join-Path $env:SystemRoot 'System32/expand.exe') @('-F:*',$cab,$extracted)
 if(@(Get-ChildItem -LiteralPath $extracted -File).Count -ne $expected.Count) { throw 'Embedded MSI cabinet file count mismatch.' }
 foreach($file in $expected) {
  $sequence=[array]::IndexOf($expected,$file)+1
  $row=@($actual.Files | Where-Object File -eq $file.Id)
  $component=@($actual.Components | Where-Object Component -eq $file.Component)
  $dir=[IO.Path]::GetDirectoryName($file.Relative).Replace('\','/')
  $expectedDir=if($dir) {Get-ProcSightMsiId 'D' $dir}else{'INSTALLFOLDER'}
  if($row.Count -ne 1 -or [int]$row[0].Sequence -ne $sequence -or ($row[0].FileName -split '\|')[-1] -cne $file.Name -or [long]$row[0].FileSize -ne $file.Length -or $component.Count -ne 1 -or $row[0].Component_ -cne $file.Component -or $component[0].ComponentId -cne (Get-ProcSightMsiGuid ('component/'+$file.Relative)) -or $component[0].KeyPath -cne $file.Id -or [int]$component[0].Attributes -ne 256 -or $component[0].Directory_ -cne $expectedDir) { throw 'MSI file/component mapping mismatch.' }
  if((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath (Join-Path $extracted $file.Id) -Algorithm SHA256).Hash) { throw 'MSI embedded payload hash mismatch.' }
 }
}
