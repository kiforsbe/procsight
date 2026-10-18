#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$CertificatePath,
    [string]$ExpectedThumbprint
)
$ErrorActionPreference = 'Stop'

function Read-ProcSightPublicSigningCertificate {
    param([Parameter(Mandatory)][string]$Path, [string]$ExpectedThumbprint)
    $file = Get-Item -LiteralPath $Path
    if ($file.PSIsContainer -or $file.Extension -ine '.cer' -or $file.Length -gt 1MB) {
        throw 'Choose one public .cer certificate file, no larger than 1 MB.'
    }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ([Security.Cryptography.X509Certificates.X509Certificate2]::GetCertContentType($bytes) -ne
        [Security.Cryptography.X509Certificates.X509ContentType]::Cert) {
        throw 'Only a public certificate is accepted; private-key containers are not supported.'
    }
    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes)
    try {
        if ($cert.HasPrivateKey -or $cert.NotBefore -gt (Get-Date) -or $cert.NotAfter -le (Get-Date)) {
            throw 'The certificate must be public-only and currently valid.'
        }
        $codeSigning = $false
        foreach ($extension in $cert.Extensions) {
            if ($extension -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
                foreach ($usage in $extension.EnhancedKeyUsages) {
                    if ($usage.Value -eq '1.3.6.1.5.5.7.3.3') { $codeSigning = $true }
                }
            }
        }
        if (!$codeSigning) { throw 'The certificate does not explicitly allow code signing.' }
        if ($ExpectedThumbprint -and $cert.Thumbprint -cne $ExpectedThumbprint) {
            throw 'The certificate changed during administrator approval. Nothing was imported.'
        }
        return $cert
    } catch { $cert.Dispose(); throw }
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
        $store = [Security.Cryptography.X509Certificates.X509Store]::new('TrustedPeople', 'LocalMachine')
        try {
            $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            # Import the validated object, never reopen a possibly replaced input file.
            $store.Add($certificate)
        } finally { $store.Dispose() }
    }
    Write-Host 'Certificate trusted in Local Machine > Trusted People. Reopen your signed MSIX to install it.'
} finally { $certificate.Dispose() }
