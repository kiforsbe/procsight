#requires -Version 7.4
[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'artifacts/certificates'
# The shared command owns key creation and public export. This wrapper supplies
# product defaults and retains the historical thumbprint-based public filename.
$provisional=Join-Path $output ('development-'+[guid]::NewGuid().ToString('N')+'.cer')
$result=& (Join-Path $script:ProcSightBuildToolsRoot 'signing/new-dev-certificate.ps1') -Subject 'CN=ProcSight Development' -PublicCertificatePath $provisional -WhatIf:$WhatIfPreference
if(!$result) { return }
$public=Join-Path $output ($result.Thumbprint+'.cer')
if(Test-Path -LiteralPath $public) { throw 'Public certificate destination already exists; the new key and provisional CER remain available.' }
Move-Item -LiteralPath $result.PublicCertificate -Destination $public
Write-Output "Development code-signing certificate created in CurrentUser\My: $($result.Thumbprint)"
Write-Output "Public certificate: $public"
Write-Output 'Trust was NOT installed. Explicitly trust this public certificate on a disposable test account before installing development MSIX packages. Never use this certificate for public distribution.'
