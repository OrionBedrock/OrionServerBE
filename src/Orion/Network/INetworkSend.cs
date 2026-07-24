using RakNet;
using RakNet.Packets.Enums;

namespace Orion.Network;

public interface INetworkSend
{
    void Send(NetworkConnection connection, ReadOnlySpan<byte> payload, Reliability reliability, bool immediate = false);

    void Disconnect(NetworkConnection connection);
}

public sealed class RakNetNetworkSend : INetworkSend
{
    public void Send(NetworkConnection connection, ReadOnlySpan<byte> payload, Reliability reliability, bool immediate = false)
        => connection.SendPacket(payload, reliability, immediate);

    public void Disconnect(NetworkConnection connection)
        => connection.Disconnect();
}
