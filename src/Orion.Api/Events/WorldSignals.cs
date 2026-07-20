using Orion.Api.Math;

namespace Orion.Api.Events;

public sealed class BlockExplodeSignal : ServerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.BlockExplode;
    public BlockPos Position { get; }
    public float Power { get; }
    public bool Cancelled { get; private set; }

    public BlockExplodeSignal(BlockPos position, float power)
    {
        Position = position;
        Power = power;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}

public sealed class ChunkLoadSignal : ServerSignal
{
    public override ServerEvent Event => ServerEvent.ChunkLoad;
    public int ChunkX { get; }
    public int ChunkZ { get; }

    public ChunkLoadSignal(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }
}

public sealed class ChunkUnloadSignal : ServerSignal
{
    public override ServerEvent Event => ServerEvent.ChunkUnload;
    public int ChunkX { get; }
    public int ChunkZ { get; }

    public ChunkUnloadSignal(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }
}
