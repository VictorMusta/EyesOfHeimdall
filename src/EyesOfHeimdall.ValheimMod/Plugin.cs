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
        // TEMP-SELFTEST (not for commit): does Unity's Mono verify RSA signatures like .NET does?
        try
        {
            var dir = @"C:\Users\victo\AppData\Local\Temp\claude\D--Dev-perso-WhereSoundComes\fa4256a8-0608-494b-af96-44d4f6459843\scratchpad\sigtest";
            var bytes = File.ReadAllBytes(Path.Combine(dir, "sample-manifest.txt"));
            var sig = File.ReadAllText(Path.Combine(dir, "sample-manifest.txt.sig"));
            var forged = File.ReadAllText(Path.Combine(dir, "sample-forged.sig"));
            var tampered = (byte[])bytes.Clone();
            tampered[8] ^= 1;
            var badSig = Convert.FromBase64String(sig);
            badSig[100] ^= 1;
            Log.LogInfo($"[SELFTEST] genuine={UpdateSignature.IsValid(bytes, sig)} tamperedManifest={UpdateSignature.IsValid(tampered, sig)} tamperedSig={UpdateSignature.IsValid(bytes, Convert.ToBase64String(badSig))} otherKey={UpdateSignature.IsValid(bytes, forged)} garbage={UpdateSignature.IsValid(bytes, "nope")} shortSig={UpdateSignature.IsValid(bytes, Convert.ToBase64String(new byte[16]))}");
        }
        catch (Exception e)
        {
            Log.LogError($"[SELFTEST] threw {e}");
        }

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

    private void OnGUI()
    {
        AutoUpdater.DrawNotice();

        // Null when Initialize failed: only the updater is alive then, nothing below is safe to draw.
        if (Radar == null)
        {
            return;
        }

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
