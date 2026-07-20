using Orion.Api.Containers;
using Orion.Api.Items;
using Orion.Api.Math;
using Orion.Api.Network;

namespace Orion.Api;

public interface IEntity
{
    long UniqueId { get; }
    ulong RuntimeId { get; }
    string TypeIdentifier { get; }
    IDimension? Dimension { get; }
    Vec3f Position { get; }
    bool IsPlayer();
    T? GetTrait<T>() where T : class;
}

public interface IPlayer : IEntity
{
    string Username { get; }
    string Xuid { get; }
    Guid Uuid { get; }
    bool IsOnline { get; }
    bool Spawned { get; }
    bool IsOperator { get; }
    Gamemode Gamemode { get; }
    void SetGamemode(Gamemode gamemode);
    void SendMessage(string message);
    void Disconnect(string reason = "");
    void Teleport(Vec3f position, IDimension? dimension = null, bool forceDimensionChange = false);
    void Send(params IOutboundPacket[] packets);
    void SetHud(HudVisibility visibility, params HudElement[] elements);
    bool DropItem(IItemStack item);
    void SyncInventoryToClient();
    IReadOnlyDictionary<int, IContainer> OpenedContainers { get; }
    void RegisterOpenContainer(int windowId, IContainer container);
    bool TryGetOpenContainer(int windowId, out IContainer? container);
    bool HasPermission(string permission);
}
