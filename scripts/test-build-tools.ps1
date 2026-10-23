#requires -Version 7.4
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
$root=Split-Path $PSScriptRoot -Parent
$checks=0
function Check([bool]$Value,[string]$Message) { if(!$Value) { throw $Message }; $script:checks++ }
function Rejected([scriptblock]$Action,[string]$Pattern) {
 $failure=$null; try { & $Action | Out-Null } catch { $failure=$_ }
 Check ($null -ne $failure -and $failure.ToString() -match $Pattern) "Expected rejection matching $Pattern; got $failure"
}
Check ([bool](Get-Command Get-ProcSightBuildToolsRoot -ErrorAction SilentlyContinue)) 'Missing shared-tool checkout resolver.'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('procsight-tools-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path "$fixture/payload/Assets/Package" -Force
try {
 Rejected { Get-ProcSightBuildToolsRoot $fixture } 'submodule update --init --recursive'
 function FixtureGit([string]$Directory,[string[]]$Arguments) {
  $messages=@(& git -C $Directory @Arguments 2>&1)
  if($LASTEXITCODE -ne 0) { throw ($messages -join "`n") }
  return ($messages -join "`n").Trim()
 }
 $null=New-Item -ItemType Directory "$fixture/tool-source","$fixture/source"
 $null=FixtureGit "$fixture/tool-source" @('init','--initial-branch=fixture')
 Set-Content "$fixture/tool-source/tool.ps1" '# Synthetic tool source'
 $null=FixtureGit "$fixture/tool-source" @('add','tool.ps1')
 $null=FixtureGit "$fixture/tool-source" @('-c','user.name=Synthetic','-c','user.email=fixture@example.invalid','commit','-m','Fixture')
 $null=FixtureGit "$fixture/source" @('init','--initial-branch=fixture')
 $null=FixtureGit "$fixture/source" @('-c','protocol.file.allow=always','submodule','add',"$fixture/tool-source",'external/winbuildtools')
 $null=FixtureGit "$fixture/source" @('-c','user.name=Synthetic','-c','user.email=fixture@example.invalid','commit','-am','Fixture')
 $null=FixtureGit "$fixture/source" @('config','submodule.external/winbuildtools.ignore','all')
 Add-Content "$fixture/source/external/winbuildtools/tool.ps1" '# Synthetic modification'
 Check (!(FixtureGit "$fixture/source" @('status','--porcelain'))) 'Fixture proves configured ignore hides modified tools from plain Git status.'
 Check ((Get-ProcSightSourceState "$fixture/source").Dirty) 'Artifact provenance must detect dirty tools despite ignore configuration.'
 foreach($name in @('ProcSight.App.exe','ProcSight.App.dll','ProcSight-LICENSE.txt','ProcSight-THIRD-PARTY-NOTICES.txt','Assets/Package/StoreLogo.png')) {
  [IO.File]::WriteAllText((Join-Path "$fixture/payload" $name),'Synthetic package fixture')
 }
 $path=Join-Path $fixture 'resolved-msi.json'
 New-ProcSightMsiConfiguration -Root $root -Folder "$fixture/payload" -Version '1.2.3' -Path $path
 $config=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
 Check ($config.schemaVersion -eq 2) 'Consumer uses explicit installer identities.'
 Check ($config.product.productCode -ceq '{74D67FBA-C97A-3ECA-1D24-0AB1D4F99C56}') 'Preserve the existing product identity derivation.'
 Check ($config.product.upgradeCode -ceq '{4DF99DE9-8E63-E6C5-2DC5-4E08D8C0D228}') 'Preserve the existing upgrade family.'
 Check ($config.components['ProcSight.App.exe'] -ceq '{5E984AEF-BC0B-4417-66BE-672C1DB0C909}') 'Preserve the existing executable component GUID.'
 Check ($config.installation.directoryName -ceq 'ProcSight' -and $config.installation.menuDirectoryName -ceq 'ProcSight') 'Consumer supplies exact install/menu directory segments.'
 Check ($config.installation.scope -ceq 'choice' -and $config.installation.defaultScope -ceq 'user') 'Installer offers scope choice, initially Just me.'
 Check ($config.installation.restartManager -ceq 'disabled') 'Preserve explicit close-before-servicing policy.'
 Check ($config.components.Count -eq 5) 'Exactly one explicit GUID for every approved payload file.'
 $map=Join-Path $fixture 'package.map.txt'
 $mapLines=@('[Files]')+@(Get-ChildItem "$fixture/payload" -File -Recurse | ForEach-Object { '"'+$_.FullName+'" "'+[IO.Path]::GetRelativePath("$fixture/payload",$_.FullName)+'"' })
 $manifestSource=Join-Path $fixture 'source-AppxManifest.xml'; Set-Content $manifestSource '<Package />'
 $mapLines+='"'+$manifestSource+'" "AppxManifest.xml"'
 $mapLines | Set-Content $map
 $expected=New-ProcSightMsixExpectation -Map $map -BuildRoot $fixture
 Check ((Get-ChildItem $expected.Payload -File -Recurse).Count -eq 5) 'MSIX expectation comes from SDK map source files, excluding container metadata.'
 Check ((Get-Content $expected.Manifest -Raw).Trim() -ceq '<Package />') 'SDK-generated authoritative manifest is provided separately.'
 ('"'+$manifestSource+'" "../escaped.txt"') | Add-Content $map
 $null=New-Item -ItemType Directory "$fixture/invalid"
 Rejected { New-ProcSightMsixExpectation -Map $map -BuildRoot "$fixture/invalid" } 'Invalid'
 Check (!(Test-Path "$fixture/escaped.txt")) 'MSIX map cannot escape the expected payload.'
 Rejected { New-ProcSightMsiConfiguration -Root $root -Folder "$fixture/payload" -Version '1.2.3' -Path $path } 'exist'
 $null=New-Item -ItemType Directory "$fixture/payload/Licenses/Runtime" -Force
 Set-Content "$fixture/payload/Licenses/Runtime/LICENSE" 'Synthetic license text'
 New-ProcSightMsiConfiguration -Root $root -Folder "$fixture/payload" -Version '1.2.3' -Path "$fixture/licenses.json"
 $licenseConfig=Get-Content "$fixture/licenses.json" -Raw | ConvertFrom-Json -AsHashtable
 Check ('Licenses/Runtime/LICENSE' -in $licenseConfig.payload.allowedFiles) 'Exact approved paths include extensionless runtime licenses.'
 Check ($licenseConfig.payload.allowedExtensions.Count -eq 0) 'Resolved MSI approval is exact rather than a broader extension allowance.'
 [IO.File]::WriteAllText("$fixture/payload/credentials.json",'Synthetic rejected input')
 Rejected { New-ProcSightMsiConfiguration -Root $root -Folder "$fixture/payload" -Version '1.2.3' -Path "$fixture/rejected.json" } 'Forbidden'
 Check (!(Test-Path "$fixture/rejected.json")) 'Payload policy rejects before emitting configuration.'
 $artifact=Join-Path $fixture 'synthetic.msi'; [IO.File]::WriteAllText($artifact,'Synthetic unsigned bytes')
 $result=Complete-ProcSightArtifact -Root $root -Artifact $artifact -BuildRoot $fixture -Inputs (Get-ProcSightPackageInputs $root Msi) -Commit ('a'*40)
 $record=Get-Content $result.Manifest -Raw | ConvertFrom-Json
 Check ($record.Commit -ceq $record.SourceCommit -and $record.SourceCommit -ceq ('a'*40)) 'Legacy commit alias uses shared source metadata.'
 Check ($record.Profile -ceq 'Msi' -and !$record.Signed -and $record.SignatureStatus -eq (Get-Content (Join-Path $fixture 'artifact-metadata.json') -Raw | ConvertFrom-Json).SignatureStatus) 'Consumer metadata preserves actual unsigned state.'
 Check ($record.SHA256 -ceq (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash) 'Shared final metadata describes actual bytes.'
 Check ($result.Artifact -ceq $artifact -and $result.Version -eq (Read-ProcSightVersion $root)) 'Release result contract remains compatible.'
 Write-Host "Shared tooling adapter checks passed: $checks"
} finally {
 $absolute=[IO.Path]::GetFullPath($fixture)
 if([IO.Path]::GetDirectoryName($absolute) -cne [IO.Path]::GetTempPath().TrimEnd('\') -or [IO.Path]::GetFileName($absolute) -notlike 'procsight-tools-*') { throw 'Unsafe adapter fixture cleanup.' }
 Remove-Item -LiteralPath $absolute -Recurse -Force
}
