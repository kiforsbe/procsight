#requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'trust-package-certificate.ps1')
$checks = 0
function Assert-Rejected([scriptblock]$Action) {
    try { & $Action | Out-Null } catch { $script:checks++; return }
    throw 'Expected certificate rejection.'
}
$folder = Join-Path ([IO.Path]::GetTempPath()) ('procsight-cert-tests-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $folder | Out-Null
$key = [Security.Cryptography.RSA]::Create(2048)
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=Synthetic package test', $key, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $oids = [Security.Cryptography.OidCollection]::new()
    [void]$oids.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
    $eku = [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $false)
    $request.CertificateExtensions.Add($eku)
    $cert = $request.CreateSelfSigned([DateTimeOffset]::Now.AddDays(-1), [DateTimeOffset]::Now.AddDays(1))
    $path = Join-Path $folder 'a & b certificate.cer'
    [IO.File]::WriteAllBytes($path, $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    $loaded = Read-ProcSightPublicSigningCertificate $path
    if ($loaded.Thumbprint -ne $cert.Thumbprint) { throw 'Certificate changed.' }; $checks++
    $loaded.Dispose()
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $path -ExpectedThumbprint ('0' * 40) }
    $wrongExtension = Join-Path $folder 'certificate.txt'
    Copy-Item -LiteralPath $path -Destination $wrongExtension
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $wrongExtension }
    Assert-Rejected { Read-ProcSightPublicSigningCertificate (Join-Path $folder 'missing.cer') }
    $bad = Join-Path $folder 'invalid.cer'; [IO.File]::WriteAllText($bad, 'not a certificate')
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $bad }
    $pfx = Join-Path $folder 'private.cer'
    [IO.File]::WriteAllBytes($pfx, $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $pfx }
    $expired = $request.CreateSelfSigned([DateTimeOffset]::Now.AddDays(-3), [DateTimeOffset]::Now.AddDays(-2))
    [IO.File]::WriteAllBytes($bad, $expired.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $bad }; $expired.Dispose()
    $future = $request.CreateSelfSigned([DateTimeOffset]::Now.AddDays(1), [DateTimeOffset]::Now.AddDays(2))
    [IO.File]::WriteAllBytes($bad, $future.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $bad }; $future.Dispose()
    $request.CertificateExtensions.Clear()
    $noEku = $request.CreateSelfSigned([DateTimeOffset]::Now.AddDays(-1), [DateTimeOffset]::Now.AddDays(1))
    [IO.File]::WriteAllBytes($bad, $noEku.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    Assert-Rejected { Read-ProcSightPublicSigningCertificate $bad }; $noEku.Dispose()
    # WhatIf must never request elevation or touch a certificate store.
    & (Join-Path $PSScriptRoot 'trust-package-certificate.ps1') -CertificatePath $path -WhatIf
    $checks++
    # Guard the persistent-key boundary even if a future wrapper accidentally
    # loses WhatIf propagation. A failing test must still never create a key.
    function New-SelfSignedCertificate { throw 'WhatIf reached persistent key creation.' }
    try {
        & (Join-Path $PSScriptRoot 'new-package-certificate.ps1') -WhatIf
        $checks++
    } finally { Remove-Item Function:/New-SelfSignedCertificate }
    $cert.Dispose()
} finally {
    $key.Dispose()
    if ([IO.Path]::GetFullPath($folder).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
}
Write-Output "Certificate helper checks passed: $checks (no trust changes)"
