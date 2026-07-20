namespace Orion.Gameplay;

/// <summary>
/// Facade for player inventory. Resolve via <c>provides: orion:inventory</c> /
/// <c>context.Services.TryGet&lt;IInventoryApi&gt;(...)</c>.
/// </summary>
public interface IInventoryApi
{
    IPlayerInventoryService Inventory { get; }
}
