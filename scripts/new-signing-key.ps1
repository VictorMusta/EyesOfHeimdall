#Requires -Version 5.1
# Creates the private key that signs update manifests. Run once.
# Installed copies only trust the matching public key (UpdateSignature.cs): lose this key and they stop updating.
$ErrorActionPreference = 'Stop'

$keyPath = Join-Path $env:USERPROFILE '.eyesofheimdall\release-signing-key.xml'
if (Test-Path $keyPath) {
    throw "A signing key already exists at $keyPath. Replacing it would cut off every installed copy; delete it yourself if that is really what you want."
}

$rsa = New-Object System.Security.Cryptography.RSACng 3072
try {
    New-Item -ItemType Directory -Force -Path (Split-Path $keyPath) | Out-Null
    [IO.File]::WriteAllText($keyPath, $rsa.ToXmlString($true))

    $public = $rsa.ExportParameters($false)
    Write-Host "Private key written to $keyPath"
    Write-Host 'Back it up somewhere safe. Never commit it or upload it to GitHub.'
    Write-Host ''
    Write-Host 'Public key for src/EyesOfHeimdall.ValheimMod/UpdateSignature.cs:'
    Write-Host ('Modulus  = ' + [Convert]::ToBase64String($public.Modulus))
    Write-Host ('Exponent = ' + [Convert]::ToBase64String($public.Exponent))
} finally {
    $rsa.Dispose()
}
