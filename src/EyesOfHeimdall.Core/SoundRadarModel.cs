namespace EyesOfHeimdall.Core;

/// <summary>
/// Engine-agnostic model behind the radar: keeps recently-heard sounds around for a short
/// window so they're visible on screen for a beat, then drops them. An engine connector feeds
/// it <see cref="SoundEvent"/>s and asks it for <see cref="SoundBlip"/>s each frame to draw.
/// </summary>
public sealed class SoundRadarModel
{
    private readonly List<SoundEvent> _events = new();

    /// <summary>Reused across calls: OnGUI-style connectors call Resolve() several times per
    /// rendered frame, and each caller consumes the result immediately — reallocating a fresh
    /// list every time would just churn the GC for no benefit.</summary>
    private readonly List<SoundBlip> _blipsBuffer = new();

    private readonly float _maxAgeSeconds;
    private readonly float _maxDistance;

    public SoundRadarModel(float maxAgeSeconds = 3f, float maxDistance = 40f)
    {
        _maxAgeSeconds = maxAgeSeconds;
        _maxDistance = maxDistance;
    }

    public float MaxDistance => _maxDistance;

    /// <summary>
    /// A sound with a known SourceId replaces any existing event from that same source (refreshing
    /// its position/timestamp) instead of stacking a second blip for what is really one entity —
    /// e.g. a creature's footstep, growl and hit sound in quick succession stay a single marker.
    /// Sounds with no SourceId (ambient/environment) always just add a new entry.
    /// </summary>
    public void Register(SoundEvent soundEvent)
    {
        if (soundEvent.SourceId != null)
        {
            for (var i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].SourceId == soundEvent.SourceId)
                {
                    _events.RemoveAt(i);
                }
            }
        }

        _events.Add(soundEvent);
    }

    /// <summary>
    /// Drops expired events and returns the ones still visible as listener-relative blips. The
    /// returned list is only valid until the next call — copy it if you need to keep it around.
    /// </summary>
    public IReadOnlyList<SoundBlip> Resolve(float nowSeconds, Vec3 listenerPosition, Vec3 listenerForward, Vec3 listenerRight)
    {
        for (var i = _events.Count - 1; i >= 0; i--)
        {
            if (nowSeconds - _events[i].CreatedAtSeconds > _maxAgeSeconds)
            {
                _events.RemoveAt(i);
            }
        }

        _blipsBuffer.Clear();
        foreach (var e in _events)
        {
            var (bearing, distance) = DirectionMath.Resolve(listenerPosition, listenerForward, listenerRight, e.WorldPosition);
            if (distance > _maxDistance)
            {
                continue;
            }

            var age = nowSeconds - e.CreatedAtSeconds;
            _blipsBuffer.Add(new SoundBlip(e.Label, e.Category, bearing, distance, e.Volume, age, _maxAgeSeconds));
        }

        return _blipsBuffer;
    }
}
