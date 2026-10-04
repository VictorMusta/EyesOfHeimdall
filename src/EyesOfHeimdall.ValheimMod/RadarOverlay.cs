using UnityEngine;
using EyesOfHeimdall.Core;

namespace EyesOfHeimdall.ValheimMod;

// Brief pings around the crosshair, drawn only while a sound is active: no permanent HUD panel.
internal static class RadarOverlay
{
    private const float ArcThickness = 24f;
    private const float MinAngularSpan = 16f;
    private const float MaxAngularSpan = 46f;
    private const float FarAlphaRatio = 0.24f;
    private const int AngularSpanBuckets = 8;
    private const float RadialSoftness = 1.5f;
    private const float AngularSoftnessRad = 0.03f;
    private const int TexturePadding = 2;

    private static readonly Dictionary<int, Texture2D> ArcTextures = new();
    private static float _arcTexturesRadius;
    private static GUIStyle? _undiscoveredStyle;

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
        var blips = radar.Resolve(
            Time.time,
            new Vec3(listenerPos.x, listenerPos.y, listenerPos.z),
            new Vec3(forward.x, forward.y, forward.z),
            new Vec3(right.x, right.y, right.z));

        var center = ScreenCenter();
        foreach (var blip in blips)
        {
            // No icon to identify the source: show nothing rather than an anonymous arc or a raw internal name.
            var icon = TrophyIcons.TryGet(blip.Category);
            if (icon == null)
            {
                continue;
            }

            var proximity = 1f - Mathf.Clamp01(blip.Distance / radar.MaxDistance);
            DrawPing(center, blip.BearingDegrees, proximity, blip.Freshness, blip.Category, icon);
        }

        GUI.color = Color.white;
    }

    // Shown while the settings panel is open, so size and opacity can be judged without waiting for a sound.
    public static void DrawPreview()
    {
        var center = ScreenCenter();
        DrawPing(center, 35f, 1f, 1f, "goblin", TrophyIcons.TryGet("goblin"));
        DrawPing(center, -35f, 0.2f, 1f, "troll", TrophyIcons.TryGet("troll"));
        GUI.color = Color.white;
    }

    private static Vector2 ScreenCenter() => new(Screen.width / 2f, Screen.height / 2f);

    private static void DrawPing(Vector2 center, float bearingDegrees, float proximity, float freshness, string category, Sprite? icon)
    {
        var radius = Mathf.Round(ModSettings.RingRadius.Value);
        var maxAlpha = ModSettings.ArcOpacity.Value;
        var alpha = Mathf.Lerp(maxAlpha * FarAlphaRatio, maxAlpha, proximity) * freshness;

        var color = CreatureColors.For(category);
        color.a = alpha;
        DrawArc(center, bearingDegrees, Mathf.Lerp(MinAngularSpan, MaxAngularSpan, proximity), radius, color);

        var iconSize = ModSettings.IconSize.Value;
        var angle = bearingDegrees * Mathf.Deg2Rad;
        var iconCenter = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * (radius + ArcThickness / 2f + iconSize / 2f);
        var iconRect = new Rect(iconCenter.x - iconSize / 2f, iconCenter.y - iconSize / 2f, iconSize, iconSize);

        if (icon != null && (!ModSettings.HideUndiscovered.Value || Discovery.IsDiscovered(category)))
        {
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(freshness) * 0.85f);
            DrawSprite(iconRect, icon);
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(freshness) * Mathf.Max(alpha, 0.6f));
            GUI.Label(iconRect, "???", UndiscoveredStyle(iconSize));
        }
    }

    private static GUIStyle UndiscoveredStyle(float iconSize)
    {
        _undiscoveredStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow,
        };

        _undiscoveredStyle.fontSize = Mathf.RoundToInt(iconSize * 16f / 34f);
        return _undiscoveredStyle;
    }

    // IMGUI has no arc primitive: the curve is baked into a texture (per-pixel polar math, anti-aliased)
    // and drawn as one piece rotated to the bearing. GUI.color tints it, so one shape serves every color.
    private static void DrawArc(Vector2 center, float bearingDegrees, float spanDegrees, float radius, Color color)
    {
        var tex = GetArcTexture(spanDegrees, radius);
        var savedMatrix = GUI.matrix;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(bearingDegrees, center);
        GUI.DrawTexture(new Rect(center.x - tex.width / 2f, center.y - (radius + ArcThickness / 2f + TexturePadding), tex.width, tex.height), tex);
        GUI.matrix = savedMatrix;
    }

    private static Texture2D GetArcTexture(float spanDegrees, float radius)
    {
        if (radius != _arcTexturesRadius)
        {
            foreach (var stale in ArcTextures.Values)
            {
                UnityEngine.Object.Destroy(stale);
            }

            ArcTextures.Clear();
            _arcTexturesRadius = radius;
        }

        var bucket = Mathf.RoundToInt(Mathf.InverseLerp(MinAngularSpan, MaxAngularSpan, spanDegrees) * (AngularSpanBuckets - 1));
        if (!ArcTextures.TryGetValue(bucket, out var tex))
        {
            tex = BuildArcTexture(Mathf.Lerp(MinAngularSpan, MaxAngularSpan, bucket / (float)(AngularSpanBuckets - 1)), radius);
            ArcTextures[bucket] = tex;
        }

        return tex;
    }

    // Only the arc's bounding box is baked, not the whole ring: cheap enough to rebuild live while a slider moves.
    private static Texture2D BuildArcTexture(float spanDegrees, float radius)
    {
        var outer = radius + ArcThickness / 2f;
        var inner = radius - ArcThickness / 2f;
        var halfSpan = spanDegrees * 0.5f * Mathf.Deg2Rad;
        var reach = halfSpan + AngularSoftnessRad;

        var width = Mathf.CeilToInt(2f * outer * Mathf.Sin(reach)) + 2 * TexturePadding;
        width += width % 2; // even width keeps texels on whole screen pixels when centred
        var height = Mathf.CeilToInt(outer - inner * Mathf.Cos(reach)) + 2 * TexturePadding;

        var pixels = new Color[width * height];
        for (var py = 0; py < height; py++)
        {
            // Texture row 0 is the bottom one; y is the upward distance from the ring's centre.
            var y = outer + TexturePadding - height + py + 0.5f;
            for (var px = 0; px < width; px++)
            {
                var x = px + 0.5f - width / 2f;
                var alpha = SmoothBand(Mathf.Sqrt(x * x + y * y), inner, outer, RadialSoftness)
                    * SmoothSpan(Mathf.Atan2(x, y), halfSpan, AngularSoftnessRad);
                pixels[py * width + px] = new Color(1f, 1f, 1f, alpha);
            }
        }

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
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
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfSpan - softness, halfSpan + softness, Mathf.Abs(angle)));
    }

    // Icons live in a shared atlas: textureRect is the sprite's place in it (sprite.rect is local and always starts at 0,0).
    private static void DrawSprite(Rect screenRect, Sprite sprite)
    {
        var texRect = sprite.textureRect;
        var tex = sprite.texture;
        var uv = new Rect(texRect.x / tex.width, texRect.y / tex.height, texRect.width / tex.width, texRect.height / tex.height);
        GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
    }
}
