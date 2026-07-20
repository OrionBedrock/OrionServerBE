using Orion.Api.Math;

namespace Orion.Api.Events;

/// <summary>Emitted before opening a block/entity container UI (chest, barrel, …).</summary>
public sealed class PlayerOpenContainerSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerOpenContainer;
    public BlockPos BlockPosition { get; }
    public string ContainerIdentifier { get; }
    public bool Cancelled { get; private set; }

    public PlayerOpenContainerSignal(IPlayer player, BlockPos blockPosition, string containerIdentifier)
        : base(player)
    {
        BlockPosition = blockPosition;
        ContainerIdentifier = containerIdentifier;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
