namespace Orion.Api.Math;

/// <summary>Stable block coordinate for plugin-facing APIs (not the Protocol wire type).</summary>
public readonly struct BlockPos(int x, int y, int z) : IEquatable<BlockPos>
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;

    public bool Equals(BlockPos other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is BlockPos other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(BlockPos left, BlockPos right) => left.Equals(right);

    public static bool operator !=(BlockPos left, BlockPos right) => !left.Equals(right);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
