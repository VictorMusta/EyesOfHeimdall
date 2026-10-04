using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace EyesOfHeimdall.ValheimMod;

internal static class AutoUpdater
{
    private const string ReleasesUrl = "https://github.com/VictorMusta/EyesOfHeimdall/releases";
    private const string ManifestName = "update-manifest.txt";
    private const int ManifestTimeoutSeconds = 10;
    private const int DownloadTimeoutSeconds = 60;
    private const float NoticeSeconds = 15f;

    private static string? _notice;
    private static float _noticeUntil;

    public static IEnumerator CheckAndInstall(string pluginDir, System.Version current)
    {
        string manifestText;
        using (var request = UnityWebRequest.Get($"{ReleasesUrl}/latest/download/{ManifestName}"))
        {
            request.timeout = ManifestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Plugin.Log.LogInfo($"[AutoUpdater] Update check skipped: {request.error}");
                yield break;
            }

            manifestText = request.downloadHandler.text;
        }

        if (!TryParse(manifestText, out var manifest))
        {
            yield break;
        }

        if (manifest.Version <= current)
        {
            Plugin.Log.LogInfo($"[AutoUpdater] Up to date ({current}).");
            yield break;
        }

        Plugin.Log.LogInfo($"[AutoUpdater] {manifest.Version} is available (running {current}), downloading.");

        // Files come from the manifest's own tag, not "latest": a release published mid-download can't mix versions.
        var downloads = new Dictionary<string, byte[]>();
        foreach (var (name, _) in manifest.Files)
        {
            using (var request = UnityWebRequest.Get($"{ReleasesUrl}/download/v{manifest.Version}/{name}"))
            {
                request.timeout = DownloadTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogWarning($"[AutoUpdater] Download of {name} failed, update not applied: {request.error}");
                    yield break;
                }

                downloads[name] = request.downloadHandler.data;
            }
        }

        try
        {
            UpdateInstaller.Install(pluginDir, manifest, downloads);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[AutoUpdater] Update to {manifest.Version} not applied: {e.Message}");
            yield break;
        }

        Plugin.Log.LogInfo($"[AutoUpdater] {manifest.Version} installed, active on next launch.");
        _notice = $"EyesOfHeimdall v{manifest.Version} installé — relance Valheim pour l'activer";
        _noticeUntil = Time.realtimeSinceStartup + NoticeSeconds;
    }

    public static void DrawNotice()
    {
        if (_notice == null || Time.realtimeSinceStartup > _noticeUntil)
        {
            return;
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(20f, 20f, 800f, 24f), _notice);
    }

    private static bool TryParse(string text, out UpdateManifest manifest)
    {
        try
        {
            manifest = UpdateManifest.Parse(text);
            return true;
        }
        catch (FormatException e)
        {
            Plugin.Log.LogWarning($"[AutoUpdater] Update manifest ignored: {e.Message}");
            manifest = null!;
            return false;
        }
    }
}
