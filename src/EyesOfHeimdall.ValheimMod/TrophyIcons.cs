using UnityEngine;

namespace EyesOfHeimdall.ValheimMod;

/// <summary>
/// Resolves a creature category to its actual in-game trophy icon (the same Sprite shown in the
/// inventory), by scanning that creature's CharacterDrop loot table for a "Trophy..." prefab.
/// Resolved once per category and cached — the live Character reference is only available at the
/// moment ZSFXPatch sees the sound, not later when RadarOverlay draws it.
/// </summary>
internal static class TrophyIcons
{
    private static readonly Dictionary<string, Sprite?> Cache = new();

    /// <summary>
    /// Cached by category, not by exact creature: some family members (e.g. Greyling) drop no
    /// trophy of their own, but share a category with one that does (Greydwarf) — so a successful
    /// resolution "sticks" for the whole category, while a miss keeps retrying on the next
    /// creature of that category instead of permanently locking it to "no icon".
    /// </summary>
    public static void EnsureCached(string category, Character character)
    {
        if (Cache.TryGetValue(category, out var existing) && existing != null)
        {
            return;
        }

        var resolved = ResolveTrophySprite(character);
        if (resolved != null || !Cache.ContainsKey(category))
        {
            Cache[category] = resolved;
        }
    }

    public static Sprite? TryGet(string category)
    {
        return Cache.TryGetValue(category, out var sprite) ? sprite : null;
    }

    private static Sprite? ResolveTrophySprite(Character character)
    {
        var drop = character.GetComponent<CharacterDrop>();
        if (drop == null)
        {
            Plugin.Log.LogInfo($"[TrophyIcons] {character.gameObject.name}: no CharacterDrop component");
            return null;
        }

        Plugin.Log.LogInfo($"[TrophyIcons] {character.gameObject.name}: drops = [{string.Join(", ", drop.m_drops.Select(d => d.m_prefab == null ? "<null>" : d.m_prefab.name))}]");

        foreach (var entry in drop.m_drops)
        {
            if (entry.m_prefab == null)
            {
                continue;
            }

            if (entry.m_prefab.name.IndexOf("Troph", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var itemDrop = entry.m_prefab.GetComponent<ItemDrop>();
            var icon = itemDrop?.m_itemData?.GetIcon();
            if (icon != null)
            {
                Plugin.Log.LogInfo($"[TrophyIcons] {character.gameObject.name}: matched {entry.m_prefab.name}, packed={icon.packed}, packingMode={icon.packingMode}, packingRotation={icon.packingRotation}, rect={icon.rect}, textureRect={icon.textureRect}, texture={icon.texture.name} {icon.texture.width}x{icon.texture.height}");
            }
            else
            {
                Plugin.Log.LogInfo($"[TrophyIcons] {character.gameObject.name}: matched {entry.m_prefab.name} but ItemDrop/icon is null (ItemDrop={itemDrop != null})");
            }

            if (icon != null && IsSimpleRect(icon))
            {
                return icon;
            }
        }

        return null;
    }

    /// <summary>
    /// Our overlay draws icons by slicing a plain axis-aligned sub-rect out of the sprite's atlas
    /// texture (see RadarOverlay.DrawSprite) — correct only for a non-rotated, non-tightly-packed
    /// sprite. A rotated or tight-packed one needs its actual mesh/UVs, which plain GUI calls can't
    /// express; better to fall back to the text label than render a cropped/rotated icon.
    /// </summary>
    private static bool IsSimpleRect(Sprite sprite)
    {
        if (!sprite.packed)
        {
            return true;
        }

        return sprite.packingRotation == SpritePackingRotation.None && sprite.packingMode == SpritePackingMode.Rectangle;
    }

    /// <summary>
    /// Debug helper (F9): dumps the shared icon atlas to the desktop so it can be inspected outside
    /// the game. The source texture is a compressed (BC7), non-readable game asset — GetPixels()
    /// would throw directly on it, so we blit it through a RenderTexture first, which works
    /// regardless of the source's read/write setting, then read that back.
    /// </summary>
    public static void DumpAtlas()
    {
        Sprite? any = null;
        foreach (var sprite in Cache.Values)
        {
            if (sprite != null)
            {
                any = sprite;
                break;
            }
        }

        if (any == null)
        {
            Plugin.Log.LogInfo("[TrophyIcons] DumpAtlas: no cached sprite yet — encounter a creature first.");
            return;
        }

        var source = any.texture;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);

        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "wheresoundcomes-icon-atlas.png");
        File.WriteAllBytes(path, readable.EncodeToPNG());
        Plugin.Log.LogInfo($"[TrophyIcons] DumpAtlas: wrote {source.name} ({source.width}x{source.height}) to {path}");
    }

    /// <summary>
    /// Debug helper (F10): walks every prefab the game knows about (ZNetScene.instance.m_prefabs —
    /// the full list, not just ones we've personally encountered), keeps the ones with a Character
    /// component, and reports each one's trophy prefab (if any) plus whether CreatureCatalog
    /// already recognizes it. Writes a full list to the desktop so the catalog can be completed in
    /// one pass instead of hunting mob by mob.
    /// </summary>
    public static void DumpAllCreatures()
    {
        if (ZNetScene.instance == null)
        {
            Plugin.Log.LogInfo("[TrophyIcons] DumpAllCreatures: ZNetScene pas encore pret — attends d'etre en jeu.");
            return;
        }

        var seen = new HashSet<string>();
        var lines = new List<string>();

        foreach (var prefab in ZNetScene.instance.m_prefabs)
        {
            if (prefab == null)
            {
                continue;
            }

            var character = prefab.GetComponent<Character>();
            if (character == null || !seen.Add(prefab.name))
            {
                continue;
            }

            var trophyName = "<aucun trophee>";
            var drop = prefab.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                foreach (var entry in drop.m_drops)
                {
                    if (entry.m_prefab != null && entry.m_prefab.name.IndexOf("Troph", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        trophyName = entry.m_prefab.name;
                        break;
                    }
                }
            }

            var (category, label) = CreatureCatalog.Identify(character);
            var status = category != null ? $"catalogue -> {category} ({label})" : "NON CATALOGUE";

            lines.Add($"{prefab.name}\ttrophee={trophyName}\t{status}");
        }

        lines.Sort(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "wheresoundcomes-creatures.txt");
        File.WriteAllLines(path, lines);
        Plugin.Log.LogInfo($"[TrophyIcons] DumpAllCreatures: {lines.Count} creatures -> {path}");
    }
}
