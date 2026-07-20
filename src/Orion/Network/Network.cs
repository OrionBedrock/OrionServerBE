namespace Orion.Network;

using System.Buffers;
using Basalt.Binary;
using Orion.Config;
using Orion.Network.Handlers;
using Orion.PluginContracts.Network;
using Orion.Plugins.Network;
using Orion.Scheduling;
using Orion.World.Coordinates;
using Orion.Player;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.RakNet;
using Orion.RakNet.Packets.Enums;
using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;
using Log = Orion.Logger.Logger;



public sealed class NetworkHandler
{
    private const int MaxPacketBatchSize = 1024 * 1024 * 8;
    private const int MaxPacketSize = 1024 * 1024 * 4;

    private readonly Server _server;
    private readonly PacketIngress _ingress;

    public Server Server => _server;

    public NetworkHandler(Server server)
    {
        _server = server;
        _ingress = new PacketIngress(server);
    }

    internal void ProcessDisconnectOnWorker(NetworkConnection connection)
    {
        if (!_server.Sessions.TryRemove(connection, out PlayerSession? session))
        {
            return;
        }

        if (_server.ConnectionCoordinator is ConnectionCoordinator coordinator)
        {
            coordinator.ReleaseSession(session);
        }

        if (session.ActiveEntity is not global::Orion.Player.Player player)
        {
            return;
        }

        AreaPlayerPresence.ClearSession(_server, player.Dimension, session);

        global::Orion.Entity.Traits.Types.EntityDespawnOptions options = new(Disconnected: true);
        _server.Emit(new PlayerLeaveSignal(player));

        (player.Dimension?.World?.Provider ?? _server.GetWorld().Provider).SavePlayerData(player.Xuid, player.WriteToNbt());

        if (player.Dimension?.World is global::Orion.World.World world)
        {
            WorldPlayerPresence.OnPlayerLeftWorld(_server, world);
        }

        string leaveMessage = $"§e{player.Username} left the server.";
        foreach (PlayerSession targetSession in _server.Sessions.Values)
        {
            targetSession.SendMessage(leaveMessage);
        }

        if (player.IsAlive && player.Dimension is not null)
        {
            player.Despawn(options);
        }

        session.ActiveEntity = null;

        PlayerListPacket removePlayer = new()
        {
            ActionType = PlayerListActionType.Remove,
            Entries =
            [
                new Orion.Protocol.Types.PlayerListEntry
                {
                    Uuid = player.Uuid
                }
            ]
        };
        _server.Broadcast(removePlayer);

        Info($"Player {player.Username} disconnected.");
    }

    public void HandleDisconnected(NetworkConnection connection)
    {
        if (_server.Properties.AreaThreadingEnabled && _server.AreaScheduler.IsActive)
        {
            _server.AreaScheduler.EnqueueAreaDisconnect(connection);
            return;
        }

        _server.Scheduler.EnqueueDisconnect(connection);
    }

    public void HandlePacket(NetworkConnection connection, ReadOnlyMemory<byte> payload)
    {
        ReadOnlySpan<byte> packetData = payload.Span;
        byte[]? decompressedBuffer = null;

        try
        {
            decompressedBuffer = ArrayPool<byte>.Shared.Rent(MaxPacketSize);
            int decompressedLength = Protocol.Io.Packet.Unframe(packetData, decompressedBuffer, out _);
            if (decompressedLength == 0) return;

            ReadOnlySpan<byte> frame = decompressedBuffer.AsSpan(0, decompressedLength);

            int offset = 0;
            BinaryReader frameReader = new(frame, ref offset);

            while (frameReader.Remaining > 0)
            {
                int packetLength = checked((int)frameReader.ReadVarUInt());
                if (packetLength <= 0 || packetLength > frameReader.Remaining) break;

                ReadOnlySpan<byte> packetBuffer = frameReader.ReadBytes(packetLength);
                if (packetBuffer.Length == 0) continue;


                try
                {
                    int offset2 = 0;
                    BinaryReader packetReader = new(packetBuffer, ref offset2);
                    uint header = packetReader.ReadVarUInt();
                    PacketId packetId = (PacketId)(header & 0x3FF);

                    _ingress.Route(connection, packetId, packetBuffer);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Packet decode/handle error ({packetBuffer.Length} bytes): {exception}");
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Network error: {exception}");
        }
        finally
        {
            if (decompressedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(decompressedBuffer);
            }
        }
    }

    internal void HandleGamePacketOnWorker(NetworkConnection connection, PacketId packetId, ReadOnlySpan<byte> packetBuffer)
    {
        CreativeInventoryLog.TryLogClientPacket(_server, connection, packetId, packetBuffer);

        PacketPipeline pipeline = _server.PacketPipeline;
        int packetIdValue = (int)packetId;
        if (pipeline.HasReceiveInterest(packetIdValue))
        {
            PacketReceiveContext receiveContext = new()
            {
                Connection = new PlayerConnectionAdapter(connection),
                PacketId = packetIdValue,
                Payload = packetBuffer.ToArray()
            };

            if (!pipeline.DispatchReceive(receiveContext))
            {
                return;
            }
        }

        try
        {
            switch (packetId)
            {
                case PacketId.Login:
                    Login.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.RequestNetworkSettings:
                    RequestNetworkSettings.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.ResourcePackClientResponse:
                    ResourcePackClientResponse.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.RequestChunkRadius:
                    RequestChunkRadius.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.SetLocalPlayerAsInitialized:
                    SetLocalPlayerAsInitialized.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.PlayerAuthInput:
                    PlayerAuthInput.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.Interact:
                    Interact.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.ContainerClose:
                    ContainerClose.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.InventoryTransaction:
                    InventoryTransaction.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.MobEquipment:
                    MobEquipment.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.PlayerAction:
                    PlayerAction.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.ItemStackRequest:
                    ItemStackRequest.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.ClientCacheStatus:
                    ClientCacheStatus.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.CommandRequest:
                    CommandRequest.Handle(_server, connection, packetBuffer);
                    break;

                case PacketId.Text:
                    Text.Handle(_server, connection, packetBuffer);
                    break;
            }
        }
        catch (Exception exception)
        {
            Log.Warn(
                LogCategory.Orion,
                "[Inv] HandleGamePacket failed packet={0} bytes={1}: {2}",
                packetId,
                packetBuffer.Length,
                exception);
            throw;
        }
    }

    public void SendPacket(NetworkConnection connection, DataPacket packet, CompressionMethod? compression = null, bool immediate = false)
    {
        SendPackets(connection, [packet], compression, immediate);
    }

    public void SendSerializedPacket(
        NetworkConnection connection,
        PacketId packetId,
        ReadOnlySpan<byte> packetPayload,
        CompressionMethod? compression = null,
        bool immediate = false)
    {
        ReadOnlyMemory<byte> body = packetPayload.ToArray();
        PacketPipeline pipeline = _server.PacketPipeline;
        int packetIdValue = (int)packetId;
        if (pipeline.HasSendInterest(packetIdValue))
        {
            PacketSendContext sendContext = new()
            {
                Connection = new PlayerConnectionAdapter(connection),
                PacketId = packetIdValue,
                Payload = body
            };

            if (!pipeline.DispatchSend(sendContext, out body))
            {
                return;
            }
        }

        using BinaryStream packetBufferStream = BinaryStream.Rent(body.Length + 16);
        using BinaryStream frameBufferStream = BinaryStream.Rent(body.Length + 32);

        BinaryWriter packetWriter = packetBufferStream;
        packetWriter.WriteVarInt((int)packetId);
        packetWriter.WriteBytes(body.Span);

        ReadOnlySpan<byte> packetData = packetWriter.GetProcessedBytes();

        BinaryWriter frameWriter = frameBufferStream;
        frameWriter.WriteVarInt(packetData.Length);
        frameWriter.WriteBytes(packetData);

        SendFrame(connection, frameWriter.GetProcessedBytes(), compression, immediate);
    }

    public void SendPackets(NetworkConnection connection, IEnumerable<DataPacket> packets, CompressionMethod? compression = null, bool immediate = false)
    {
        using BinaryStream packetBufferStream = BinaryStream.Rent(MaxPacketSize);
        using BinaryStream frameBufferStream = BinaryStream.Rent(MaxPacketBatchSize);
        BinaryWriter frameWriter = frameBufferStream;
        PacketPipeline pipeline = _server.PacketPipeline;

        foreach (DataPacket packet in packets)
        {
            packetBufferStream.Offset = 0;
            BinaryWriter packetWriter = packetBufferStream;
            Protocol.Io.Packet.Serialize(packet, packetWriter);

            ReadOnlySpan<byte> packetData = packetWriter.GetProcessedBytes();
            int packetIdValue = (int)Protocol.Io.PacketCodec.GetId(packet);

            if (pipeline.HasSendInterest(packetIdValue))
            {
                // Full framed packet bytes (header + body) for hooks; replacement replaces entire packetData.
                PacketSendContext sendContext = new()
                {
                    Connection = new PlayerConnectionAdapter(connection),
                    PacketId = packetIdValue,
                    Payload = packetData.ToArray()
                };

                if (!pipeline.DispatchSend(sendContext, out ReadOnlyMemory<byte> replaced))
                {
                    continue;
                }

                packetData = replaced.Span;
            }

            frameWriter.WriteVarInt(packetData.Length);
            frameWriter.WriteBytes(packetData);
        }

        ReadOnlySpan<byte> frame = frameWriter.GetProcessedBytes();
        if (frame.Length == 0)
        {
            return;
        }

        SendFrame(connection, frame, compression, immediate);
    }

    private void SendFrame(NetworkConnection connection, ReadOnlySpan<byte> frame, CompressionMethod? compression, bool immediate = false)
    {
        CompressionMethod method = compression ?? _server.Properties.CompressionMethod;
        if (method != CompressionMethod.None && method != CompressionMethod.NotPresent && frame.Length < _server.Properties.CompressionThreshold)
        {
            method = CompressionMethod.None;
        }

        if (method == CompressionMethod.Snappy)
        {
            throw new NotSupportedException("Snappy compression is not supported.");
        }

        int reserve = method == CompressionMethod.Zlib ? 1024 * 1024 : 0;
        int headerSize = method == CompressionMethod.NotPresent ? 1 : 2;
        byte[] compressedBuffer = ArrayPool<byte>.Shared.Rent(frame.Length + reserve + headerSize);

        try
        {
            compressedBuffer[0] = 0xFE;

            int payloadOffset = 1;
            if (method != CompressionMethod.NotPresent)
            {
                compressedBuffer[1] = (byte)method;
                payloadOffset = 2;
            }

            int payloadLength;
            if (method == CompressionMethod.Zlib)
            {
                payloadLength = Protocol.Io.Packet.Compress(frame, compressedBuffer.AsSpan(payloadOffset));
            }
            else
            {
                frame.CopyTo(compressedBuffer.AsSpan(payloadOffset));
                payloadLength = frame.Length;
            }

            connection.SendPacket(compressedBuffer.AsSpan(0, payloadOffset + payloadLength), Reliability.ReliableOrdered, immediate);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressedBuffer);
        }
    }

    private static CompressionMethod GetCompressionMethod(string? value)
    {
        if (value is not null && value.Equals("snappy", StringComparison.OrdinalIgnoreCase))
        {
            return CompressionMethod.Snappy;
        }

        return CompressionMethod.Zlib;
    }

}










