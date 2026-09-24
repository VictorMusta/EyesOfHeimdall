namespace EyesOfHeimdall.ValheimMod;

/// <summary>
/// Tracks which creature categories the local player has actually fought at least once —
/// persisted to disk (not per-save) so the "bestiary" stays unlocked across characters/worlds,
/// matching how a real hunter would remember what a troll looks like once they've fought one.
/// Gates only the trophy icon reveal (see RadarOverlay) — the directional arc itself always shows
/// in its real category color, since hiding direction would undermine the accessibility purpose.
/// </summary>
internal static class Discovery
{
    private static readonly HashSet<string> DiscoveredCategories = new();
    private static bool _loaded;

    private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "eyesofheimdall.discovered.txt");

    public static bool IsDiscovered(string category)
    {
        EnsureLoaded();
        return DiscoveredCategories.Contains(category);
    }

    public static void MarkDiscovered(string category)
    {
        EnsureLoaded();
        if (!DiscoveredCategories.Add(category))
        {
            return;
        }

        try
        {
            File.AppendAllText(FilePath, category + Environment.NewLine);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Discovery] Could not persist discovery of '{category}': {e.Message}");
        }

        Plugin.Log.LogInfo($"[Discovery] Nouvelle creature decouverte: {category}");
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        if (!File.Exists(FilePath))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(FilePath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                DiscoveredCategories.Add(trimmed);
            }
        }
    }
}
