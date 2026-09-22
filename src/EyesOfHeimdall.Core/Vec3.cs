namespace EyesOfHeimdall.Core;

/// <summary>Engine-agnostic 3D vector so Core never depends on a specific game engine's math types.</summary>
public readonly struct Vec3
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public Vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public float Length()
    {
        return (float)System.Math.Sqrt(X * X + Y * Y + Z * Z);
    }
}
