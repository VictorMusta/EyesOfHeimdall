param(
    [string]$ValheimPath = ""
)

$ErrorActionPreference = "Stop"

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

Write-Host "=== Desinstallation de EyesOfHeimdall ===" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($ValheimPath)) {
    Write-Host "Recherche de Valheim..."
    $ValheimPath = Find-ValheimPath
}

if ([string]::IsNullOrWhiteSpace($ValheimPath) -or -not (Test-Path (Join-Path $ValheimPath "valheim.exe"))) {
    Write-Host "Valheim introuvable automatiquement." -ForegroundColor Yellow
    $ValheimPath = Read-Host "Colle le chemin complet du dossier Valheim (ex: C:\Program Files (x86)\Steam\steamapps\common\Valheim)"
    if (-not (Test-Path (Join-Path $ValheimPath "valheim.exe"))) {
        Write-Host "Ce dossier ne contient pas valheim.exe. Desinstallation annulee." -ForegroundColor Red
        Read-Host "Appuie sur Entree pour fermer"
        exit 1
    }
}

Write-Host "Valheim trouve : $ValheimPath" -ForegroundColor Green
Write-Host ""

# 1. Le mod
$pluginDir = Join-Path $ValheimPath "BepInEx\plugins\EyesOfHeimdall"
if (Test-Path $pluginDir) {
    Remove-Item -Path $pluginDir -Recurse -Force
    Write-Host "Mod EyesOfHeimdall supprime." -ForegroundColor Green
} else {
    Write-Host "Le mod n'etait pas installe (rien a supprimer)."
}

# 2. Sa config (optionnelle)
$configFile = Join-Path $ValheimPath "BepInEx\config\com.eyesofheimdall.valheim.cfg"
if (Test-Path $configFile) {
    Remove-Item -Path $configFile -Force
    Write-Host "Fichier de configuration supprime."
}

Write-Host ""
$removeBepInEx = Read-Host "Supprimer aussi BepInEx entierement (le chargeur de mods) ? Fais-le seulement si tu n'as pas d'autres mods installes. [o/N]"
if ($removeBepInEx -eq "o" -or $removeBepInEx -eq "O") {
    $bepinexDir = Join-Path $ValheimPath "BepInEx"
    $doorstop = Join-Path $ValheimPath "winhttp.dll"
    $doorstopConfig = Join-Path $ValheimPath "doorstop_config.ini"

    if (Test-Path $bepinexDir) { Remove-Item -Path $bepinexDir -Recurse -Force }
    if (Test-Path $doorstop) { Remove-Item -Path $doorstop -Force }
    if (Test-Path $doorstopConfig) { Remove-Item -Path $doorstopConfig -Force }
    Write-Host "BepInEx supprime -- Valheim est revenu a son etat sans mods." -ForegroundColor Green
} else {
    Write-Host "BepInEx conserve (il ne fait rien sans mod dedans)."
}

Write-Host ""
Write-Host "=== Desinstallation terminee ! ===" -ForegroundColor Green
Read-Host "Appuie sur Entree pour fermer"
