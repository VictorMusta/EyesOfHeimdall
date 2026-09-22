namespace EyesOfHeimdall.Core;

/// <summary>One sound that started playing somewhere in the world, captured engine-side and handed to Core.</summary>
public sealed class SoundEvent
{
    public SoundEvent(string id, Vec3 worldPosition, string label, string category, float createdAtSeconds, float volume = 1f, string? sourceId = null)
    {
        Id = id;
        WorldPosition = worldPosition;
        Label = label;
        Category = category;
        CreatedAtSeconds = createdAtSeconds;
        Volume = volume;
        SourceId = sourceId;
    }

    public string Id { get; }
    public Vec3 WorldPosition { get; }

    /// <summary>Human-facing text, e.g. "Sanglier".</summary>
    public string Label { get; }

    /// <summary>Stable machine key for grouping/coloring, e.g. "boar". Engine connectors own the mapping to an actual color.</summary>
    public string Category { get; }

    public float CreatedAtSeconds { get; }
    public float Volume { get; }

    /// <summary>
    /// Stable per-entity identity (e.g. the creature instance), when the engine connector can supply
    /// one. Lets the radar merge several sounds from the same source into a single blip instead of
    /// stacking one icon per sound. Null means "no known source" (ambient/environment) — never merged.
    /// </summary>
    public string? SourceId { get; }
}

/// <summary>A sound's position and loudness resolved relative to the listener, ready to draw.</summary>
public readonly struct SoundBlip
{
    public SoundBlip(string label, string category, float bearingDegrees, float distance, float volume, float ageSeconds, float maxAgeSeconds)
    {
        Label = label;
        Category = category;
        BearingDegrees = bearingDegrees;
        Distance = distance;
        Volume = volume;
        AgeSeconds = ageSeconds;
        MaxAgeSeconds = maxAgeSeconds;
    }

    /// <summary>0 = straight ahead of the listener, 90 = right, -90 = left, 180/-180 = behind.</summary>
    public float BearingDegrees { get; }
    public float Distance { get; }
    public float Volume { get; }
    public string Label { get; }
    public string Category { get; }
    public float AgeSeconds { get; }
    public float MaxAgeSeconds { get; }

    /// <summary>1 when freshly created, fading to 0 as it approaches MaxAgeSeconds. Drives opacity/size in the overlay.</summary>
    public float Freshness => System.Math.Max(0f, 1f - AgeSeconds / MaxAgeSeconds);
}
