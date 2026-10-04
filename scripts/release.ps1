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

if (-not (Test-Path $NotesFile)) { throw "Notes file not found: $NotesFile" }
$NotesFile = (Resolve-Path $NotesFile).Path

$source = Get-Content 'src/EyesOfHeimdall.ValheimMod/Plugin.cs' -Raw
if ($source -notmatch 'public const string Version = "(\d+\.\d+\.\d+)";') { throw 'Plugin.Version not found in Plugin.cs' }
$version = $Matches[1]
# The updater rebuilds the tag from the parsed version, so "0.2.00" would point it at a tag that does not exist.
if (([version]$version).ToString() -ne $version) { throw "Plugin.Version '$version' is not in canonical form" }
$tag = "v$version"
if (-not $Title) { $Title = $tag }

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
# No BOM: the mod parses this file as plain key=value lines.
[IO.File]::WriteAllLines($manifest, $manifestLines, (New-Object Text.UTF8Encoding($false)))

$zip = Join-Path $stage "EyesOfHeimdall-$tag.zip"
$zipContent = @(Get-ChildItem 'installer' -File | ForEach-Object { $_.FullName }) + ($dlls | ForEach-Object { Join-Path $stage $_ })
Compress-Archive -Path $zipContent -DestinationPath $zip

$assets = @($zip, $manifest) + ($dlls | ForEach-Object { Join-Path $stage $_ })
gh release create $tag @assets --target $head --title $Title --notes-file $NotesFile
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed' }

# What installed copies will actually fetch must be exactly what was just built.
$published = Join-Path $stage 'published-manifest.txt'
$expected = (Get-FileHash $manifest -Algorithm SHA256).Hash
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        Invoke-WebRequest "$repoUrl/releases/latest/download/$manifestName" -OutFile $published -UseBasicParsing
        if ((Get-FileHash $published -Algorithm SHA256).Hash -eq $expected) {
            Write-Host "Released $tag - update manifest is live."
            exit 0
        }
    } catch {
    }
    Start-Sleep -Seconds 5
}
throw "Release $tag was published, but the 'latest' update manifest does not match it. Check the release assets on GitHub."
