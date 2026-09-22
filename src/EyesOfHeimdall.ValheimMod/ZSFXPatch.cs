using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using EyesOfHeimdall.Core;

namespace EyesOfHeimdall.ValheimMod;

/// <summary>
/// Every ZSFX.Play() call is one sound starting somewhere in the world — creatures, footsteps,
/// impacts, ambience. We hook the tail end of it purely to observe (never to change behaviour)
/// and hand the position + a human-readable label + category to the engine-agnostic radar model.
/// </summary>
[HarmonyPatch(typeof(ZSFX), nameof(ZSFX.Play))]
internal static class ZSFXPatch
{
    private static void Postfix(ZSFX __instance)
    {
        if (Player.m_localPlayer == null)
        {
            return;
        }

        if (Plugin.HideOwnSounds.Value && IsPlayerCreator(__instance))
        {
            return;
        }

        var position = __instance.transform.position;
        var (label, category, sourceId) = ResolveIdentity(__instance);

        Plugin.Radar.Register(new SoundEvent(
            id: Guid.NewGuid().ToString(),
            worldPosition: new Vec3(position.x, position.y, position.z),
            label: label,
            category: category,
            createdAtSeconds: Time.time,
            volume: __instance.m_maxVol,
            sourceId: sourceId));
    }

    private const float NearbyCharacterSearchRadius = 8f;
    private static readonly List<Character> NearbyCharacterBuffer = new();

    /// <summary>
    /// sourceId lets several sounds from the same creature (footstep, growl, hit...) collapse into
    /// one blip instead of stacking — GetInstanceID() is stable for that GameObject's lifetime and
    /// needs no Valheim networking knowledge (ZDOID etc.) to be a valid per-entity key here.
    /// </summary>
    private static (string label, string category, string? sourceId) ResolveIdentity(ZSFX sfx)
    {
        var character = sfx.GetComponentInParent<Character>() ?? FindNearestCharacter(sfx.transform.position);
        var (category, creatureLabel) = CreatureCatalog.Identify(character);
        if (category != null && creatureLabel != null)
        {
            TrophyIcons.EnsureCached(category, character!);
            return (creatureLabel, category, character!.GetInstanceID().ToString());
        }

        return (ResolveFallbackLabel(sfx), "environment", null);
    }

    /// <summary>
    /// Hit/death effects go through EffectList.Create, which only parents to the source when the
    /// effect entry has m_attach set — plenty (blood, death poofs, impact sounds) don't, since they're
    /// meant to stay put at the impact point rather than follow the creature. When the hierarchy walk
    /// finds nothing, fall back to "closest tracked Character to where the sound actually happened".
    /// </summary>
    private static Character? FindNearestCharacter(Vector3 position)
    {
        NearbyCharacterBuffer.Clear();
        Character.GetCharactersInRange(position, NearbyCharacterSearchRadius, NearbyCharacterBuffer);

        Character? closest = null;
        var closestSqrDistance = float.MaxValue;
        foreach (var candidate in NearbyCharacterBuffer)
        {
            var sqrDistance = (candidate.transform.position - position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    private static string ResolveFallbackLabel(ZSFX sfx)
    {
        if (!string.IsNullOrEmpty(sfx.m_closedCaptionToken))
        {
            return sfx.m_closedCaptionToken.TrimStart('$');
        }

        var clip = sfx.GetComponent<AudioSource>()?.clip;
        if (clip != null && !string.IsNullOrEmpty(clip.name))
        {
            return clip.name;
        }

        return sfx.gameObject.name;
    }

    /// <summary>
    /// Delegates to ZSFX's own private IsPlayerCreator() rather than re-deriving it: footstep/grass
    /// effects are Instantiate()'d with no parent (see FootStep.cs), so a GetComponentInParent
    /// check on Player misses them entirely. The game already tracks the real creator via
    /// SetSoundEffectCreator()/m_sfxCreator (with a ZNetView-owner fallback for sounds that don't
    /// go through that path) — reusing it is more robust than reimplementing that logic here.
    ///
    /// This runs on every single sound in the game (including footsteps, the highest-frequency
    /// case), so the reflected MethodInfo is resolved once and cached — Traverse.Create(...).Method(...)
    /// re-resolves it via reflection on every call, which is wasteful on a hot path.
    /// </summary>
    private static readonly MethodInfo IsPlayerCreatorMethod =
        AccessTools.Method(typeof(ZSFX), "IsPlayerCreator") ?? throw new MissingMethodException("ZSFX.IsPlayerCreator");

    private static bool IsPlayerCreator(ZSFX sfx)
    {
        return (bool)IsPlayerCreatorMethod.Invoke(sfx, null);
    }
}
