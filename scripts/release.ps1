#Requires -Version 5.1
# Builds and publishes a GitHub release that installed copies can auto-update from.
# The version comes from Plugin.Version; the tag, the manifest and the DLLs all derive from it.
param(
    [Parameter(Mandatory = $true)] [string]$NotesFile,
    [string]$Title
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$repoUrl = 'https://github.com/VictorMusta/EyesOfHeimdall'
$project = 'src/EyesOfHeimdall.ValheimMod/EyesOfHeimdall.ValheimMod.csproj'
$buildDir = 'src/EyesOfHeimdall.ValheimMod/bin/Release/net472'
$dlls = 'EyesOfHeimdall.Core.dll', 'EyesOfHeimdall.ValheimMod.dll'
$manifestName = 'update-manifest.txt'
$keyPath = Join-Path $env:USERPROFILE '.eyesofheimdall\release-signing-key.xml'

if (-not (Test-Path $NotesFile)) { throw "Notes file not found: $NotesFile" }
$NotesFile = (Resolve-Path $NotesFile).Path

$source = Get-Content 'src/EyesOfHeimdall.ValheimMod/Plugin.cs' -Raw
if ($source -notmatch 'public const string Version = "(\d+\.\d+\.\d+)";') { throw 'Plugin.Version not found in Plugin.cs' }
$version = $Matches[1]
# The updater rebuilds the tag from the parsed version, so "0.2.00" would point it at a tag that does not exist.
if (([version]$version).ToString() -ne $version) { throw "Plugin.Version '$version' is not in canonical form" }
$tag = "v$version"
if (-not $Title) { $Title = $tag }

# Installed copies reject any manifest not signed by the key whose public half they embed.
# Publishing with another key would silently strand them, so never generate a new key to get past this.
if (-not (Test-Path $keyPath)) { throw "Signing key not found at $keyPath. Restore it from its backup: installed copies accept no other key." }
$rsa = New-Object System.Security.Cryptography.RSACng
$rsa.FromXmlString([IO.File]::ReadAllText($keyPath))
$publicModulus = [Convert]::ToBase64String($rsa.ExportParameters($false).Modulus)
if (-not (Get-Content 'src/EyesOfHeimdall.ValheimMod/UpdateSignature.cs' -Raw).Contains("`"$publicModulus`"")) {
    throw 'The signing key does not match the public key embedded in UpdateSignature.cs.'
}

if (git status --porcelain) { throw 'Working tree is not clean: commit first, the release must match a commit.' }
git fetch origin --tags --quiet
if ($LASTEXITCODE -ne 0) { throw 'git fetch failed' }
$head = git rev-parse HEAD
if ($head -ne (git rev-parse '@{u}')) { throw 'HEAD is not pushed: push first, the tag is created on the remote.' }
if (git tag --list $tag) { throw "Tag $tag already exists: bump Plugin.Version." }

dotnet build $project -c Release --no-incremental
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$stage = Join-Path $root 'dist/release'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

$manifestLines = @("version=$version")
foreach ($dll in $dlls) {
    Copy-Item (Join-Path $buildDir $dll) $stage
    $hash = (Get-FileHash (Join-Path $stage $dll) -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestLines += "$dll=$hash"
}
$manifest = Join-Path $stage $manifestName
# No BOM, and only version + "<name>.dll=<sha256>" lines: copies as old as 0.2.0 reject any other line.
[IO.File]::WriteAllLines($manifest, $manifestLines, (New-Object Text.UTF8Encoding($false)))

$signature = "$manifest.sig"
$signatureBytes = $rsa.SignData([IO.File]::ReadAllBytes($manifest), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
[IO.File]::WriteAllText($signature, [Convert]::ToBase64String($signatureBytes))
$rsa.Dispose()

$zip = Join-Path $stage "EyesOfHeimdall-$tag.zip"
$zipContent = @(Get-ChildItem 'installer' -File | ForEach-Object { $_.FullName }) + ($dlls | ForEach-Object { Join-Path $stage $_ })
Compress-Archive -Path $zipContent -DestinationPath $zip

$assets = @($zip, $manifest, $signature) + ($dlls | ForEach-Object { Join-Path $stage $_ })
gh release create $tag @assets --target $head --title $Title --notes-file $NotesFile
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed' }

# What installed copies will actually fetch must be exactly what was just built and signed.
$live = @{
    "$repoUrl/releases/latest/download/$manifestName"   = $manifest
    "$repoUrl/releases/download/$tag/$manifestName.sig" = $signature
}
for ($attempt = 1; $attempt -le 5; $attempt++) {
    $matching = 0
    foreach ($url in $live.Keys) {
        try {
            $published = Join-Path $stage 'published.tmp'
            Invoke-WebRequest $url -OutFile $published -UseBasicParsing
            if ((Get-FileHash $published -Algorithm SHA256).Hash -eq (Get-FileHash $live[$url] -Algorithm SHA256).Hash) { $matching++ }
        } catch {
        }
    }
    if ($matching -eq $live.Count) {
        Write-Host "Released $tag - signed update manifest is live."
        exit 0
    }
    Start-Sleep -Seconds 5
}
throw "Release $tag was published, but the live update manifest or its signature does not match it. Check the release assets on GitHub."
