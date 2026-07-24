using Orion.Entity;
using Orion.Network;
using Orion.Protocol.Packets;
using Orion.World.Chunk;

namespace Orion.Player.Traits;

/// <summary>
/// Core client chunk streaming (Chebyshev). Does not replace ChunkLoadingTrait simulation tickets.
/// </summary>
public sealed class PlayerChunkStreamingTrait : IEntityTrait, IChunkPositionAware
{
    public const int ChunksPerTick = 64;

    private readonly Player _player;
    private readonly HashSet<long> _loaded = new();
    private readonly List<DataPacket> _sendBuffer = new(ChunksPerTick);
    private int _radius = 8;
    private int _centerX;
    private int _centerZ;
    private int _ring;
    private int _ringIndex;
    private bool _started;
    private bool _detached;

    public PlayerChunkStreamingTrait(Player player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _centerX = player.ChunkX;
        _centerZ = player.ChunkZ;
        _radius = Math.Max(1, player.ViewDistanceChebyshev);
        player.BindViewDistanceListener(ApplyViewDistance);
    }

    public int Radius => _radius;

    public int LoadedCount => _loaded.Count;

    public void Start()
    {
        if (_detached)
        {
            return;
        }

        _started = true;
        _ring = 0;
        _ringIndex = 0;
        SendPublisherUpdate();
        Tick();
    }

    public void ApplyViewDistance(int chebyshevRadius)
    {
        if (_detached)
        {
            return;
        }

        _radius = Math.Max(1, chebyshevRadius);
        _ring = 0;
        _ringIndex = 0;
        UnloadOutOfRange(clearClient: true);
        if (_started)
        {
            SendPublisherUpdate();
        }
    }

    public void OnChunkPositionChanged(int chunkX, int chunkZ)
    {
        if (_detached || !_started)
        {
            return;
        }

        if (chunkX == _centerX && chunkZ == _centerZ)
        {
            return;
        }

        _centerX = chunkX;
        _centerZ = chunkZ;
        _ring = 0;
        _ringIndex = 0;
        SendPublisherUpdate();
        UnloadOutOfRange(clearClient: true);
    }

    public void Tick()
    {
        if (_detached || !_started)
        {
            return;
        }

        _sendBuffer.Clear();
        while (_sendBuffer.Count < ChunksPerTick && NextRingPosition(out int x, out int z))
        {
            long hash = Hash(x, z);
            if (!_loaded.Add(hash))
            {
                continue;
            }

            _sendBuffer.Add(new LevelChunkPacket
            {
                ChunkX = x,
                ChunkZ = z,
                Dimension = _player.Dimension.Type,
                SubChunkCount = VoidLevelChunkEncoder.VoidSubChunkCount,
                CacheEnabled = false,
                RawPayload = VoidLevelChunkEncoder.EncodePayload(),
            });
        }

        if (_sendBuffer.Count > 0)
        {
            PacketSendGate.Send(_player.Session.Connection, _sendBuffer);
        }
    }

    public void OnDetach()
    {
        _detached = true;
        _started = false;
        UnloadOutOfRange(clearClient: true, force: true);
        _loaded.Clear();
    }

    private void SendPublisherUpdate()
    {
        int blockX = (_centerX << 4) + 8;
        int blockZ = (_centerZ << 4) + 8;
        int blockY = (int)Math.Floor(_player.SpawnY);
        PacketSendGate.Send(
            _player.Session.Connection,
            new NetworkChunkPublisherUpdatePacket
            {
                CoordinateX = blockX,
                CoordinateY = blockY,
                CoordinateZ = blockZ,
                Radius = ChunkViewMath.PublisherRadiusBlocks(_radius),
                SavedChunks = [],
            });
    }

    private void UnloadOutOfRange(bool clearClient, bool force = false)
    {
        if (_loaded.Count == 0)
        {
            return;
        }

        List<long> remove = [];
        foreach (long hash in _loaded)
        {
            Unhash(hash, out int x, out int z);
            if (!force && InRange(x, z))
            {
                continue;
            }

            if (clearClient)
            {
                PacketSendGate.Send(
                    _player.Session.Connection,
                    new LevelChunkPacket
                    {
                        ChunkX = x,
                        ChunkZ = z,
                        Dimension = _player.Dimension.Type,
                        SubChunkCount = 0,
                        CacheEnabled = false,
                        RawPayload = VoidLevelChunkEncoder.EncodeUnloadPayload(),
                    });
            }

            remove.Add(hash);
        }

        foreach (long hash in remove)
        {
            _loaded.Remove(hash);
        }
    }

    private bool InRange(int chunkX, int chunkZ)
        => Math.Max(Math.Abs(chunkX - _centerX), Math.Abs(chunkZ - _centerZ)) <= _radius;

    private bool NextRingPosition(out int x, out int z)
    {
        while (_ring <= _radius)
        {
            if (_ring == 0)
            {
                _ring = 1;
                _ringIndex = 0;
                x = _centerX;
                z = _centerZ;
                return true;
            }

            int r = _ring;
            int perimeterLength = 8 * r;
            if (_ringIndex >= perimeterLength)
            {
                _ring++;
                _ringIndex = 0;
                continue;
            }

            int sideLength = 2 * r;
            int index = _ringIndex++;
            int offsetX;
            int offsetZ;
            if (index < sideLength)
            {
                offsetX = -r + index;
                offsetZ = -r;
            }
            else if (index < sideLength * 2)
            {
                int i = index - sideLength;
                offsetX = r;
                offsetZ = -r + i;
            }
            else if (index < sideLength * 3)
            {
                int i = index - (sideLength * 2);
                offsetX = r - i;
                offsetZ = r;
            }
            else
            {
                int i = index - (sideLength * 3);
                offsetX = -r;
                offsetZ = r - i;
            }

            x = _centerX + offsetX;
            z = _centerZ + offsetZ;
            return true;
        }

        x = 0;
        z = 0;
        return false;
    }

    private static long Hash(int x, int z) => ((long)x << 32) ^ (uint)z;

    private static void Unhash(long hash, out int x, out int z)
    {
        x = (int)(hash >> 32);
        z = (int)hash;
    }
}

/// <summary>
/// Bridges streaming trait to <see cref="PacketSender"/> without circular refs in construction.
/// </summary>
public static class PacketSendGate
{
    private static PacketSender? _sender;

    public static void Bind(PacketSender sender)
        => _sender = sender ?? throw new ArgumentNullException(nameof(sender));

    public static void Send(RakNet.NetworkConnection connection, DataPacket packet)
    {
        if (_sender is null)
        {
            throw new InvalidOperationException("PacketSendGate is not bound.");
        }

        _sender.Send(connection, packet);
    }

    public static void Send(RakNet.NetworkConnection connection, IReadOnlyList<DataPacket> packets)
    {
        if (_sender is null)
        {
            throw new InvalidOperationException("PacketSendGate is not bound.");
        }

        _sender.Send(connection, packets);
    }
}
