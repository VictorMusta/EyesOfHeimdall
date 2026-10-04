using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace EyesOfHeimdall.ValheimMod;

internal static class AutoUpdater
{
    private const string ReleasesUrl = "https://github.com/VictorMusta/EyesOfHeimdall/releases";
    private const string ManifestName = "update-manifest.txt";
    private const float StartupDelaySeconds = 10f;
    private const int MetadataTimeoutSeconds = 10;
    private const int DownloadTimeoutSeconds = 60;
    private const int MaxManifestBytes = 16 * 1024;
    private const float NoticeSeconds = 15f;

    private static string? _notice;
    private static float _noticeUntil;

    public static IEnumerator CheckAndInstall(string pluginDir, System.Version current)
    {
        // Wait out the game's own startup: the check must never compete with loading, and it is done long before a world opens.
        yield return new WaitForSecondsRealtime(StartupDelaySeconds);

        byte[] manifestBytes;
        using (var request = UnityWebRequest.Get($"{ReleasesUrl}/latest/download/{ManifestName}"))
        {
            request.timeout = MetadataTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Plugin.Log.LogInfo($"[AutoUpdater] Update check skipped: {request.error}");
                yield break;
            }

            manifestBytes = request.downloadHandler.data ?? Array.Empty<byte>();
        }

        if (!TryParse(manifestBytes, out var manifest))
        {
            yield break;
        }

        // The common case ends here, after a single tiny request.
        if (manifest.Version <= current)
        {
            Plugin.Log.LogInfo($"[AutoUpdater] Up to date ({current}).");
            yield break;
        }

        // Everything below comes from the manifest's own tag, not "latest": a release published mid-update can't mix versions.
        var tagUrl = $"{ReleasesUrl}/download/v{manifest.Version}";

        string signature;
        using (var request = UnityWebRequest.Get($"{tagUrl}/{ManifestName}.sig"))
        {
            request.timeout = MetadataTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Plugin.Log.LogWarning($"[AutoUpdater] {manifest.Version} ignored, its signature could not be fetched: {request.error}");
                yield break;
            }

            signature = request.downloadHandler.text ?? "";
        }

        if (!UpdateSignature.IsValid(manifestBytes, signature))
        {
            Plugin.Log.LogError($"[AutoUpdater] {manifest.Version} ignored: its manifest is not signed with the EyesOfHeimdall release key.");
            yield break;
        }

        Plugin.Log.LogInfo($"[AutoUpdater] {manifest.Version} is available (running {current}), downloading.");

        var downloads = new Dictionary<string, byte[]>();
        foreach (var (name, _) in manifest.Files)
        {
            using (var request = UnityWebRequest.Get($"{tagUrl}/{name}"))
            {
                request.timeout = DownloadTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogWarning($"[AutoUpdater] Download of {name} failed, update not applied: {request.error}");
                    yield break;
                }

                downloads[name] = request.downloadHandler.data ?? Array.Empty<byte>();
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

    private static bool TryParse(byte[] manifestBytes, out UpdateManifest manifest)
    {
        manifest = null!;

        if (manifestBytes.Length == 0 || manifestBytes.Length > MaxManifestBytes)
        {
            Plugin.Log.LogWarning($"[AutoUpdater] Update manifest ignored: unexpected size ({manifestBytes.Length} bytes).");
            return false;
        }

        try
        {
            manifest = UpdateManifest.Parse(Encoding.UTF8.GetString(manifestBytes));
            return true;
        }
        catch (FormatException e)
        {
            Plugin.Log.LogWarning($"[AutoUpdater] Update manifest ignored: {e.Message}");
            return false;
        }
    }
}
