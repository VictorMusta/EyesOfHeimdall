# Product Brief — WhereSoundComes

*Brief léger, issu de la session de brainstorming du 2026-09-22
(`_bmad-output/brainstorming/brainstorm-sound-source-visualizer-2026-09-22/`).
Cadrage volontairement condensé pour livrer un POC le jour même — à approfondir
en PRD si le concept est validé après tests.*

## Problème

Une personne n'entendant que d'une oreille perd les indices interauraux
(différences de temps et de niveau entre les deux oreilles) qui permettent de
localiser une source sonore dans l'espace. Dans un jeu vidéo, cela veut dire :
aucune indication de la direction d'où vient un bruit (ennemi, danger,
information d'ambiance) — un désavantage que les joueurs à deux oreilles ne
perçoivent même pas.

## Solution

Un canal de substitution visuel : une interface qui affiche en temps réel la
direction et la distance des sons actifs autour du joueur, sous forme de radar
compact à l'écran. Objectif à terme : un cœur (calcul direction/distance,
gestion du cycle de vie des sons affichés) indépendant du moteur de jeu, avec
un connecteur léger par moteur/jeu.

## Décisions de cadrage

| Sujet | Décision | Pourquoi |
|---|---|---|
| Source des données | Mod/plugin avec accès aux positions 3D réelles des sources audio (pas d'analyse du mix audio de sortie) | Précision réelle (direction + distance exactes), cohérent avec la vision "interface adaptative par moteur" |
| Moteur du POC | Unity, via un jeu existant | Rapide à instrumenter, écosystème de modding mature |
| Jeu cible | **Valheim** | Installé et moddable (BepInEx/Harmony), écosystème de mods stable au moment du test malgré la sortie de la 1.0 le 9 septembre 2026 |
| Point d'accroche technique | `ZSFX.Play()` (système de sound effects de Valheim) | Chaque son du jeu y transite ; position via `transform.position`, libellé via `m_closedCaptionToken` déjà fourni par le jeu |

## Portée du POC (aujourd'hui)

- Patch Harmony sur `ZSFX.Play()` → capture position + libellé de chaque son.
- `SoundRadarModel` (Core, agnostique du moteur) → garde les sons actifs ~3s,
  résout direction/distance par rapport à la caméra du joueur.
- `RadarOverlay` (OnGUI) → radar circulaire, points colorés par type de son,
  taille/opacité qui décroît avec l'âge du son.

## Hors scope (pour aujourd'hui)

- Analyse de mix audio générique (approche B) — à explorer si on veut couvrir
  des jeux non moddables.
- Retour haptique — piste identifiée (Valheim vibre déjà via `m_useVibration`)
  mais pas dans le POC.
- Deuxième connecteur moteur — le POC valide l'architecture "cœur +
  connecteur" avec un seul moteur ; la généralisation est la prochaine étape
  si le concept est validé à l'usage.

## Validation

À valider en conditions réelles : la copine de l'utilisateur joue avec le mod
actif et donne un retour sur la lisibilité du radar (position à l'écran,
taille des points, vitesse de disparition) et sur son utilité réelle en jeu.
