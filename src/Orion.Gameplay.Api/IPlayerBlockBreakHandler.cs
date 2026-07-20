using Orion.Api;
using Orion.Api.Math;

namespace Orion.Gameplay;

/// <summary>
/// Opt-in block crack / destroy. Implemented by VanillaMining.
/// </summary>
public interface IPlayerBlockBreakHandler
{
    void OnStartDestroy(IPlayer player, BlockPos pos, int face, ulong tick);

    void OnContinueDestroy(IPlayer player, BlockPos pos, int face, ulong tick);

    void OnCrack(IPlayer player, BlockPos pos, int face, ulong tick);

    void OnAbortDestroy(IPlayer player, BlockPos pos, int face);

    void OnPredictDestroy(IPlayer player, BlockPos pos, int face, ulong tick);

    void OnCreativeDestroy(IPlayer player, BlockPos pos, int face);
}
