using Orion.Api;
using Orion.Api.Items;
using Orion.Api.Math;

namespace Orion.Gameplay;

/// <summary>
/// Opt-in block/air item use (place, interact, use-on-block). Implemented by VanillaBuilding.
/// </summary>
public interface IPlayerBlockUseHandler
{
    bool TryUseOnBlock(IPlayer player, BlockPos blockPos, int face, BlockPos placePos, IItemStack? held);

    bool TryUseOnAir(IPlayer player, IItemStack? held);
}
