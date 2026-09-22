namespace EyesOfHeimdall.Core;

/// <summary>
/// Turns a world position into a bearing/distance relative to a listener. The engine side is
/// responsible for handing in <paramref name="listenerForward"/>/<paramref name="listenerRight"/>
/// already flattened to the horizontal plane and normalized — Core stays free of any notion of
/// "up axis" or engine-specific transform API.
/// </summary>
public static class DirectionMath
{
    public static (float bearingDegrees, float distance) Resolve(
        Vec3 listenerPosition, Vec3 listenerForward, Vec3 listenerRight, Vec3 sourcePosition)
    {
        var toSource = sourcePosition - listenerPosition;
        var distance = toSource.Length();

        var forwardDot = Dot(toSource, listenerForward);
        var rightDot = Dot(toSource, listenerRight);
        var bearing = (float)(System.Math.Atan2(rightDot, forwardDot) * (180.0 / System.Math.PI));

        return (bearing, distance);
    }

    private static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
