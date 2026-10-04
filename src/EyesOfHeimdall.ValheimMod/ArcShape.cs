using UnityEngine;

namespace EyesOfHeimdall.ValheimMod;

// Per-pixel opacity of one arc of the ring, centred on "up". Only the arc's bounding box is computed,
// not the whole ring: cheap enough to rebuild live while the ring-size slider moves.
internal static class ArcShape
{
    public const float Thickness = 24f;
    public const int Padding = 2;

    private const float RadialSoftness = 1.5f;
    private const float AngularSoftnessRad = 0.03f;

    // Rows run bottom to top, as Texture2D.SetPixels expects. The top row sits (radius + Thickness / 2 + Padding) above the ring's centre.
    public static float[] Alpha(float spanDegrees, float radius, out int width, out int height)
    {
        var outer = radius + Thickness / 2f;
        var inner = radius - Thickness / 2f;
        var halfSpan = spanDegrees * 0.5f * Mathf.Deg2Rad;
        var reach = halfSpan + AngularSoftnessRad;

        width = Mathf.CeilToInt(2f * outer * Mathf.Sin(reach)) + 2 * Padding;
        width += width % 2; // even width keeps texels on whole screen pixels when centred
        height = Mathf.CeilToInt(outer - inner * Mathf.Cos(reach)) + 2 * Padding;

        var alpha = new float[width * height];
        for (var py = 0; py < height; py++)
        {
            var y = outer + Padding - height + py + 0.5f;
            for (var px = 0; px < width; px++)
            {
                var x = px + 0.5f - width / 2f;
                alpha[py * width + px] =
                    SmoothBand(Mathf.Sqrt(x * x + y * y), inner, outer, RadialSoftness)
                    * SmoothSpan(Mathf.Atan2(x, y), halfSpan, AngularSoftnessRad);
            }
        }

        return alpha;
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
}
