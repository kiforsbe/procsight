#requires -Version 7.4
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$CertificatePath,
    [string]$ExpectedThumbprint
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'packaging/Common.ps1')
. (Join-Path $script:ProcSightBuildToolsRoot 'signing/Certificates.ps1')

function Read-ProcSightPublicSigningCertificate {
    param([Parameter(Mandatory)][string]$Path, [string]$ExpectedThumbprint)
    $arguments=@{Path=$Path}
    if($ExpectedThumbprint) { $arguments.ExpectedThumbprint=$ExpectedThumbprint }
    Read-WbtPublicSigningCertificate @arguments
}

# Dot-sourcing exposes validation for synthetic, store-free tests.
if ($MyInvocation.InvocationName -eq '.') { return }
if (!$CertificatePath) { throw 'Supply -CertificatePath or drop a .cer onto trust-package-certificate.cmd.' }
$resolved = (Get-Item -LiteralPath $CertificatePath).FullName
$certificate = Read-ProcSightPublicSigningCertificate $resolved $ExpectedThumbprint
try {
    Write-Host "Certificate: $($certificate.Subject)"
    Write-Host "Thumbprint:  $($certificate.Thumbprint)"
    Write-Host "Expires:     $($certificate.NotAfter.ToString('u'))"
    Write-Host 'This grants machine-wide Trusted People trust. Use only a certificate you trust.'
    if (!$PSCmdlet.ShouldProcess($certificate.Thumbprint, 'Trust signing certificate in LocalMachine\TrustedPeople')) { return }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $admin = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)
    } finally { $identity.Dispose() }
    if (!$admin) {
        # Encode a script, with literal-quoted arguments, rather than concatenate shell commands.
        $scriptLiteral = $PSCommandPath.Replace("'", "''")
        $fileLiteral = $resolved.Replace("'", "''")
        $thumbprint = $certificate.Thumbprint
        $command = "try { & '$scriptLiteral' -CertificatePath '$fileLiteral' -ExpectedThumbprint '$thumbprint'; exit 0 } catch { exit 1 }"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        try {
            $process = Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -Verb RunAs -WindowStyle Hidden `
                -ArgumentList @('-NoProfile', '-EncodedCommand', $encoded) -Wait -PassThru
        } catch { throw 'Administrator approval was cancelled or could not be started. No trust change was completed.' }
        if ($process.ExitCode -ne 0) { throw 'Certificate trust failed. Retry from an administrator PowerShell window to see the detailed error.' }
    } else {
        # The shared helper imports the validated object, not the mutable file.
        $null=Import-WbtPublicSigningCertificate -Certificate $certificate -ExpectedThumbprint $certificate.Thumbprint -Confirm:$false
    }
    Write-Host 'Certificate trusted in Local Machine > Trusted People. Reopen your signed MSIX to install it.'
} finally { $certificate.Dispose() }
