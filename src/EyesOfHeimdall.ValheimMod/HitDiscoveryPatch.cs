using HarmonyLib;

namespace EyesOfHeimdall.ValheimMod;

/// <summary>
/// Character.RPC_Damage already contains the exact check we need internally
/// (hit.GetAttacker() == Player.m_localPlayer) — patching it here marks that creature's category
/// as discovered the moment the local player actually lands a hit on it, so its trophy icon can
/// start showing on the radar from then on (see Discovery, RadarOverlay).
/// </summary>
[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class HitDiscoveryPatch
{
    private static void Postfix(Character __instance, HitData hit)
    {
        if (Player.m_localPlayer == null || hit.GetAttacker() != Player.m_localPlayer)
        {
            return;
        }

        var (category, _) = CreatureCatalog.Identify(__instance);
        if (category == null)
        {
            return;
        }

        TrophyIcons.EnsureCached(category, __instance);
        Discovery.MarkDiscovered(category);
    }
}
