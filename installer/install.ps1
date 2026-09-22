param(
    [string]$ValheimPath = ""
)

$ErrorActionPreference = "Stop"
$BepInExVersion = "5.4.2350"
$BepInExUrl = "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/$BepInExVersion/"

function Find-ValheimPath {
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
        "C:\Steam\steamapps\common\Valheim",
        "D:\Steam\steamapps\common\Valheim",
        "D:\SteamLibrary\steamapps\common\Valheim",
        "E:\SteamLibrary\steamapps\common\Valheim"
    )
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "valheim.exe")) { return $c }
    }

    # Look through every Steam library listed in libraryfolders.vdf
    $steamPaths = @("C:\Program Files (x86)\Steam", "C:\Steam")
    foreach ($steam in $steamPaths) {
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            $content = Get-Content $vdf -Raw
            $matches = [regex]::Matches($content, '"path"\s*"([^"]+)"')
            foreach ($m in $matches) {
                $libPath = $m.Groups[1].Value -replace '\\\\', '\'
                $candidate = Join-Path $libPath "steamapps\common\Valheim"
                if (Test-Path (Join-Path $candidate "valheim.exe")) { return $candidate }
            }
        }
    }

    return $null
}

Write-Host "=== Installation de EyesOfHeimdall pour Valheim ===" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($ValheimPath)) {
    Write-Host "Recherche de Valheim..."
    $ValheimPath = Find-ValheimPath
}

if ([string]::IsNullOrWhiteSpace($ValheimPath) -or -not (Test-Path (Join-Path $ValheimPath "valheim.exe"))) {
    Write-Host "Valheim introuvable automatiquement." -ForegroundColor Yellow
    $ValheimPath = Read-Host "Colle le chemin complet du dossier Valheim (ex: C:\Program Files (x86)\Steam\steamapps\common\Valheim)"
    if (-not (Test-Path (Join-Path $ValheimPath "valheim.exe"))) {
        Write-Host "Ce dossier ne contient pas valheim.exe. Installation annulee." -ForegroundColor Red
        Read-Host "Appuie sur Entree pour fermer"
        exit 1
    }
}

Write-Host "Valheim trouve : $ValheimPath" -ForegroundColor Green
Write-Host ""

# 1. BepInEx (mod loader) -- skip if already installed
if (-not (Test-Path (Join-Path $ValheimPath "BepInEx\core"))) {
    Write-Host "Installation de BepInEx (chargeur de mods)..."
    $zipPath = Join-Path $env:TEMP "bepinex_valheim_$BepInExVersion.zip"
    Invoke-WebRequest -Uri $BepInExUrl -OutFile $zipPath

    $extractPath = Join-Path $env:TEMP "bepinex_valheim_extract"
    if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    Copy-Item -Path (Join-Path $extractPath "BepInExPack_Valheim\*") -Destination $ValheimPath -Recurse -Force
    Write-Host "BepInEx installe." -ForegroundColor Green
} else {
    Write-Host "BepInEx deja present, pas besoin de le reinstaller."
}

# 2. Le mod lui-meme
$pluginDir = Join-Path $ValheimPath "BepInEx\plugins\EyesOfHeimdall"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Copy-Item -Path (Join-Path $scriptDir "EyesOfHeimdall.Core.dll") -Destination $pluginDir -Force
Copy-Item -Path (Join-Path $scriptDir "EyesOfHeimdall.ValheimMod.dll") -Destination $pluginDir -Force

Write-Host ""
Write-Host "=== Installation terminee ! ===" -ForegroundColor Green
Write-Host "Lance Valheim normalement -- le radar de sons s'active tout seul."
Write-Host "Astuce : les sons du personnage lui-meme (pas, coups...) sont caches par defaut."
Write-Host ""
Read-Host "Appuie sur Entree pour fermer"
