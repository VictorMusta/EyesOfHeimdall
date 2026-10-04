using BepInEx;
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
    public const string Version = "0.3.0";

    /// <summary>Shared with the static Harmony patch, which has no instance of its own to hold state on.</summary>
    public static SoundRadarModel Radar { get; private set; } = null!;

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
        ModSettings.AutoUpdate = Config.Bind(
            "Updates",
            "AutoUpdate",
            true,
            "Au lancement, vérifier s'il existe une version plus récente sur GitHub et l'installer (elle s'active au lancement suivant).");

        if (ModSettings.AutoUpdate.Value)
        {
            StartCoroutine(AutoUpdater.CheckAndInstall(Path.GetDirectoryName(Info.Location), new System.Version(Version)));
        }
    }

    private void Initialize()
    {
        ModSettings.Bind(Config);

        // The settings panel moves sliders every frame; it saves once on release instead of on every change.
        Config.SaveOnConfigSet = false;

        Radar = new SoundRadarModel(maxAgeSeconds: 1.5f, maxDistance: ModSettings.DetectionRange.Value);
        ModSettings.DetectionRange.SettingChanged += (_, _) => Radar.MaxDistance = ModSettings.DetectionRange.Value;

        new Harmony(Guid).PatchAll();
        Logger.LogInfo($"{Name} {Version} loaded — listening for ZSFX.Play()");
    }

    private bool _drawingFailed;

    private void OnGUI()
    {
        AutoUpdater.DrawNotice();

        // Radar is null when Initialize failed: only the updater is alive then, nothing below is safe to draw.
        if (Radar == null || _drawingFailed)
        {
            return;
        }

        try
        {
            if (ModSettings.Enabled.Value)
            {
                RadarOverlay.Draw(Radar);

                if (SettingsPanel.IsOpen)
                {
                    RadarOverlay.DrawPreview();
                }
            }

            SettingsPanel.Draw(Config);
        }
        catch (Exception e) when (e is not UnityEngine.ExitGUIException)
        {
            // OnGUI runs several times per frame: an error repeated at that rate floods the log for the whole session.
            _drawingFailed = true;
            Logger.LogError($"Drawing disabled until the next launch after an error: {e}");
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
