using Orion.Api.Math;

namespace Orion.Api.Events;

public sealed class PlayerBreakBlockSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerBreakBlock;
    public BlockPos BlockPosition { get; }
    public int BlockFace { get; }
    public bool Cancelled { get; private set; }

    public PlayerBreakBlockSignal(IPlayer player, BlockPos blockPosition, int blockFace) : base(player)
    {
        BlockPosition = blockPosition;
        BlockFace = blockFace;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
