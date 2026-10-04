using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using EyesOfHeimdall.Core;

namespace EyesOfHeimdall.ValheimMod;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.eyesofheimdall.valheim";
    public const string Name = "EyesOfHeimdall";
    // Single source of truth for the release tag and the update manifest (see scripts/release.ps1).
    public const string Version = "0.2.0";

    /// <summary>Shared with the static Harmony patch, which has no instance of its own to hold state on.</summary>
    public static SoundRadarModel Radar { get; private set; } = null!;

    /// <summary>Footsteps, weapon swings, grunts, eating/drinking — the player already knows about their own sounds.</summary>
    public static ConfigEntry<bool> HideOwnSounds { get; private set; } = null!;

    /// <summary>Static access for the Harmony patch / helper classes, which have no BaseUnityPlugin instance of their own.</summary>
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;

        // First and on its own: if a game update breaks the patches below, the mod can still fetch its own fix.
        StartUpdateCheck();
        Initialize();
    }

    private void StartUpdateCheck()
    {
        var autoUpdate = Config.Bind(
            "Updates",
            "AutoUpdate",
            true,
            "Au lancement, vérifier s'il existe une version plus récente sur GitHub et l'installer (elle s'active au lancement suivant).");

        if (autoUpdate.Value)
        {
            StartCoroutine(AutoUpdater.CheckAndInstall(Path.GetDirectoryName(Info.Location), new System.Version(Version)));
        }
    }

    private void Initialize()
    {
        HideOwnSounds = Config.Bind(
            "Filtering",
            "HideOwnSounds",
            true,
            "Cacher du radar les sons produits par le personnage du joueur lui-même (pas, coups, voix...).");

        var detectionRange = Config.Bind(
            "Detection",
            "DetectionRangeMeters",
            80f,
            "Distance max (en mètres) à laquelle un son est encore affiché sur le radar.");

        Radar = new SoundRadarModel(maxAgeSeconds: 1.5f, maxDistance: detectionRange.Value);

        new Harmony(Guid).PatchAll();
        Logger.LogInfo($"{Name} {Version} loaded — listening for ZSFX.Play()");
    }

    private void OnGUI()
    {
        AutoUpdater.DrawNotice();

        if (Radar != null)
        {
            RadarOverlay.Draw(Radar);
        }
    }

    /// <summary>
    /// F9: dump the shared icon atlas to the desktop for inspection (see TrophyIcons.DumpAtlas).
    /// F10: dump every creature the game knows about, with its trophy and catalog status (see
    /// TrophyIcons.DumpAllCreatures) — the full list in one pass, not just what's been encountered.
    /// </summary>
    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
        {
            TrophyIcons.DumpAtlas();
        }

        if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F10))
        {
            TrophyIcons.DumpAllCreatures();
        }
    }
}
