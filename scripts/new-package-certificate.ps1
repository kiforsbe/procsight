[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'artifacts/certificates'; New-Item -ItemType Directory -Path $output -Force | Out-Null
$cert=New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=ProcSight Development' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyExportPolicy NonExportable -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(1)
$public=Join-Path $output ($cert.Thumbprint+'.cer')
Export-Certificate -Cert $cert -FilePath $public | Out-Null
Write-Output "Development code-signing certificate created in CurrentUser\My: $($cert.Thumbprint)"
Write-Output "Public certificate: $public"
Write-Output 'Trust was NOT installed. Explicitly trust this public certificate on a disposable test account before installing development MSIX packages. Never use this certificate for public distribution.'
