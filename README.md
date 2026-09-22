# EyesOfHeimdall

Interface adaptative qui visualise l'origine des sons dans un jeu, pour compenser
l'absence de localisation auditive (ITD/ILD) chez une personne n'entendant que
d'une oreille.

## Concept

- **Core** (`src/EyesOfHeimdall.Core`) : moteur-agnostique. Modèle de "sons actifs"
  (`SoundRadarModel`) + calcul de direction/distance relative à un auditeur
  (`DirectionMath`). Ne dépend d'aucun moteur de jeu.
- **Connecteurs par moteur** : un projet par moteur qui branche Core sur les
  événements audio réels et dessine l'overlay. Premier connecteur : Valheim.

## POC actuel : `EyesOfHeimdall.ValheimMod`

Mod BepInEx/Harmony pour Valheim (Unity, Mono).

- Patch sur `ZSFX.Play()` (le système de sound effects du jeu) : à chaque son
  joué, on capture sa position 3D. La créature source est identifiée via la
  hiérarchie du son ou, à défaut (effets d'impact non attachés), via la
  créature vivante la plus proche du point sonore.
- `Core.SoundRadarModel` garde les sons actifs ~1,5s (fusionnés par créature :
  plusieurs sons d'une même source ne produisent qu'un seul repère) et les
  résout en direction/distance par rapport à la caméra du joueur.
- `RadarOverlay` dessine, façon Fortnite, un arc de cercle bref autour du
  viseur à chaque son (pas de panneau permanent) avec l'icône réelle du
  trophée de la créature (extraite de l'atlas d'icônes du jeu). Un son sans
  trophée connu n'affiche rien plutôt qu'un nom technique.
- `CreatureCatalog` couvre l'intégralité des créatures du jeu (base +
  Mistlands/Ashlands/Deep North), chacune avec sa couleur.

### Build & test

Prérequis : SDK .NET, Valheim installé (chemin par défaut dans le `.csproj`,
override avec `-p:ValheimDir=...`), BepInEx installé dans le dossier du jeu.

```bash
dotnet build src/EyesOfHeimdall.ValheimMod/EyesOfHeimdall.ValheimMod.csproj -c Release
```

Le build copie automatiquement le mod dans
`<Valheim>/BepInEx/plugins/EyesOfHeimdall/` — il suffit de relancer le jeu.
Logs : `<Valheim>/BepInEx/LogOutput.log`.

Réglages (générés au premier lancement dans
`BepInEx/config/com.eyesofheimdall.valheim.cfg`) : portée de détection,
masquage des sons produits par le joueur lui-même.

### Prochaines étapes possibles

- Distinguer danger réel (créature hostile en chasse) vs bruit de fond
  (faune passive/fuyante).
- Retour haptique directionnel (vibration manette asymétrique gauche/droite).
- Indiquer si un son se rapproche ou s'éloigne dans le temps.
- Deuxième connecteur moteur (Godot/Unity générique) pour valider que Core
  tient sa promesse "un seul cœur, plusieurs moteurs".

## Installateur

Un installateur autonome (DLL compilées + script PowerShell) pour installer
le mod sur une autre machine sans setup de dev est généré dans `dist/`.

## BMAD

Ce projet a été initialisé et cadré avec BMAD-METHOD (`_bmad/`,
`.claude/skills/bmad-*`). Le journal de la session de maturation de l'idée est
dans `_bmad-output/brainstorming/`.
