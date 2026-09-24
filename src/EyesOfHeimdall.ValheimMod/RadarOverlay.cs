using UnityEngine;
using EyesOfHeimdall.Core;

namespace EyesOfHeimdall.ValheimMod;

/// <summary>
/// Fortnite-style directional pings around the player's own reticle, instead of a persistent
/// radar panel — nothing is drawn until a sound actually happens, so there's no permanent HUD
/// element that reads as a wallhack. Each ping is a soft curved arc at the sound's bearing,
/// closer/louder sounds wider and a little brighter, fading out fast, with the source's icon
/// shown right next to it.
/// </summary>
internal static class RadarOverlay
{
    private const float RingRadius = 150f;
    private const float ArcThickness = 24f;
    private const float MinAngularSpan = 16f;
    private const float MaxAngularSpan = 46f;
    private const float MinAlpha = 0.12f;
    private const float MaxAlpha = 0.5f;
    private const float IconSize = 34f;

    public static void Draw(SoundRadarModel radar)
    {
        var cam = GameCamera.instance;
        if (cam == null || Player.m_localPlayer == null)
        {
            return;
        }

        var forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        var right = cam.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 1e-6f)
        {
            right = Vector3.right;
        }
        right.Normalize();

        var listenerPos = cam.transform.position;
        var listenerForward = new Vec3(forward.x, forward.y, forward.z);
        var listenerRight = new Vec3(right.x, right.y, right.z);

        var blips = radar.Resolve(Time.time, new Vec3(listenerPos.x, listenerPos.y, listenerPos.z), listenerForward, listenerRight);

        var center = new Vector2(Screen.width / 2f, Screen.height / 2f);

        foreach (var blip in blips)
        {
            // No trophy icon for this creature (yet) -> skip entirely rather than show an arc with
            // nothing identifying it, or a raw internal name as text. Nothing beats a broken look.
            var icon = TrophyIcons.TryGet(blip.Category);
            if (icon == null)
            {
                continue;
            }

            var normDist = Mathf.Clamp01(blip.Distance / radar.MaxDistance);
            var proximity = 1f - normDist; // 1 = right on top of the listener, 0 = at MaxDistance

            var angularSpan = Mathf.Lerp(MinAngularSpan, MaxAngularSpan, proximity);
            var alpha = Mathf.Lerp(MinAlpha, MaxAlpha, proximity) * blip.Freshness;

            var color = CreatureColors.For(blip.Category);
            color.a = alpha;

            DrawArc(center, blip.BearingDegrees, angularSpan, color);

            if (Discovery.IsDiscovered(blip.Category))
            {
                DrawIcon(center, blip, icon);
            }
            else
            {
                DrawUndiscovered(center, blip, alpha);
            }
        }

        GUI.color = Color.white;
    }

    // --- Arc rendering -------------------------------------------------
    //
    // IMGUI has no arc primitive, and stacking many small rotated rectangles (the first attempt)
    // shows visible seams/overlap banding. Instead we bake the curve itself into a texture once —
    // real per-pixel polar math, anti-aliased — and every frame just draw that single texture,
    // rotated as one piece to the sound's bearing. Tint (GUI.color) supplies the creature color, so
    // one shape texture per angular-width "bucket" covers every color.

    private const int ArcTextureSize = 340;
    private const int AngularSpanBuckets = 8;
    private const float RadialSoftness = 1.5f;   // px, anti-aliasing on the inner/outer edge
    private const float AngularSoftnessRad = 0.03f; // radians, anti-aliasing on the two tapered ends

    private static readonly Dictionary<int, Texture2D> ArcTextureCache = new();

    private static void DrawArc(Vector2 center, float bearingDegrees, float angularSpanDegrees, Color color)
    {
        var tex = GetArcTexture(angularSpanDegrees);
        var savedMatrix = GUI.matrix;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(bearingDegrees, center);
        GUI.DrawTexture(new Rect(center.x - ArcTextureSize / 2f, center.y - ArcTextureSize / 2f, ArcTextureSize, ArcTextureSize), tex);
        GUI.matrix = savedMatrix;
    }

    private static Texture2D GetArcTexture(float angularSpanDegrees)
    {
        var t = Mathf.InverseLerp(MinAngularSpan, MaxAngularSpan, angularSpanDegrees);
        var bucket = Mathf.RoundToInt(t * (AngularSpanBuckets - 1));

        if (!ArcTextureCache.TryGetValue(bucket, out var tex))
        {
            var span = Mathf.Lerp(MinAngularSpan, MaxAngularSpan, bucket / (float)(AngularSpanBuckets - 1));
            tex = BuildArcTexture(span);
            ArcTextureCache[bucket] = tex;
        }

        return tex;
    }

    /// <summary>
    /// Renders a white, alpha-shaped annulus sector: opaque between [RingRadius - thickness/2,
    /// RingRadius + thickness/2] and within [-halfSpan, +halfSpan] of "up" (texture-local bearing
    /// 0), softened at both boundaries. "Up" in a plain Texture2D (SetPixel origin bottom-left) is
    /// exactly what GUI.DrawTexture displays as up, so this lines up with our screen bearing
    /// convention with no extra flip.
    /// </summary>
    private static Texture2D BuildArcTexture(float angularSpanDegrees)
    {
        var tex = new Texture2D(ArcTextureSize, ArcTextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var center = (ArcTextureSize - 1) / 2f;
        var outerRadius = RingRadius + ArcThickness / 2f;
        var innerRadius = RingRadius - ArcThickness / 2f;
        var halfSpanRad = angularSpanDegrees * 0.5f * Mathf.Deg2Rad;

        var pixels = new Color[ArcTextureSize * ArcTextureSize];
        for (var y = 0; y < ArcTextureSize; y++)
        {
            var dy = y - center; // positive = "up" in the displayed image
            for (var x = 0; x < ArcTextureSize; x++)
            {
                var dx = x - center;
                var dist = Mathf.Sqrt(dx * dx + dy * dy);
                var angle = Mathf.Atan2(dx, dy); // 0 = up, matches bearing convention (sin=x, cos=y)

                var radialAlpha = SmoothBand(dist, innerRadius, outerRadius, RadialSoftness);
                var angularAlpha = SmoothSpan(angle, halfSpanRad, AngularSoftnessRad);

                pixels[y * ArcTextureSize + x] = new Color(1f, 1f, 1f, radialAlpha * angularAlpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static float SmoothBand(float value, float lo, float hi, float softness)
    {
        var rising = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo - softness, lo + softness, value));
        var falling = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hi - softness, hi + softness, value));
        return Mathf.Min(rising, falling);
    }

    private static float SmoothSpan(float angle, float halfSpan, float softness)
    {
        var distanceFromCenter = Mathf.Abs(angle);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfSpan - softness, halfSpan + softness, distanceFromCenter));
    }

    // --- Source icon ------------------------------------------------------

    private static Vector2 IconPosition(Vector2 center, SoundBlip blip)
    {
        var angleRad = blip.BearingDegrees * Mathf.Deg2Rad;
        var labelRadius = RingRadius + ArcThickness / 2f + 16f;
        return center + new Vector2(Mathf.Sin(angleRad), -Mathf.Cos(angleRad)) * labelRadius;
    }

    private static void DrawIcon(Vector2 center, SoundBlip blip, Sprite icon)
    {
        var pos = IconPosition(center, blip);
        var alpha = Mathf.Clamp01(blip.Freshness) * 0.85f;

        GUI.color = new Color(1f, 1f, 1f, alpha);
        DrawSprite(new Rect(pos.x - IconSize / 2f, pos.y - IconSize / 2f, IconSize, IconSize), icon);
    }

    /// <summary>Creature recognized (category has a trophy) but not yet fought — a deliberate
    /// "undiscovered" placeholder, not a raw internal name, so it reads as a bestiary entry to fill
    /// in rather than a broken label.</summary>
    private static GUIStyle? _undiscoveredStyle;

    private static void DrawUndiscovered(Vector2 center, SoundBlip blip, float arcAlpha)
    {
        if (_undiscoveredStyle == null)
        {
            _undiscoveredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 16,
            };
        }

        var pos = IconPosition(center, blip);
        var alpha = Mathf.Clamp01(blip.Freshness) * Mathf.Max(arcAlpha, 0.6f);

        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(pos.x - IconSize / 2f, pos.y - IconSize / 2f, IconSize, IconSize), "???", _undiscoveredStyle);
    }

    /// <summary>
    /// Sprites live in a shared atlas texture. sprite.rect is the sprite's own LOCAL bounds
    /// (always starts at 0,0) — sprite.textureRect is its actual position within the atlas, which
    /// is the one that has to feed the UV coords, or every icon samples the same top-left corner.
    /// </summary>
    private static void DrawSprite(Rect screenRect, Sprite sprite)
    {
        var texRect = sprite.textureRect;
        var tex = sprite.texture;
        var uv = new Rect(texRect.x / tex.width, texRect.y / tex.height, texRect.width / tex.width, texRect.height / tex.height);
        GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
    }
}
