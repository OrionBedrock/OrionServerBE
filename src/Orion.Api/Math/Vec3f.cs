namespace Orion.Api.Math;

/// <summary>Stable float vector for plugin-facing APIs (not the Protocol wire type).</summary>
public readonly struct Vec3f(float x = 0, float y = 0, float z = 0) : IEquatable<Vec3f>
{
    public static readonly Vec3f Zero = new(0, 0, 0);

    public float X { get; } = x;
    public float Y { get; } = y;
    public float Z { get; } = z;

    public bool Equals(Vec3f other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Vec3f other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Vec3f left, Vec3f right) => left.Equals(right);

    public static bool operator !=(Vec3f left, Vec3f right) => !left.Equals(right);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
