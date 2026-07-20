namespace Orion.Player.Traits;

using System.Text;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.Scheduling;
using Orion.Api.Traits;

using Entity = Orion.Entity.Entity;
using Orion.Entity.Traits.Types;
using Orion.Entity.Traits;

public sealed class DebugTrait : PlayerTrait, ISessionTickableTrait
{
    private const double TargetTps = 20.0;
    private const ulong SendIntervalTicks = 20;

    public new static string Identifier => "debug";
    public new static readonly EntityIdentifier[] Types = [EntityIdentifier.Player];

    private ulong _lastSentTick;
    private double _averageMspt;
    private DebugHudMode _mode = DebugHudMode.Full;

    public DebugTrait(Entity entity) : base(entity)
    {
    }

    public override void OnSpawn(EntitySpawnOptions details)
    {
        _lastSentTick = Player.Dimension?.World is Tickable tickable ? tickable.TickValue : 0;
        _averageMspt = 0;
        _mode = DebugHudMode.Full;
    }

    public void OnSessionTick()
    {
        ulong currentTick = Player.Dimension?.World is Tickable tickable ? tickable.TickValue : 0;
        OnTick(new TraitOnTickDetails(currentTick, 1));
    }

    public override void OnTick(TraitOnTickDetails details)
    {
        if (!Player.IsAlive || _mode == DebugHudMode.Off || details.CurrentTick - _lastSentTick < SendIntervalTicks)
        {
            return;
        }

        try
        {
            global::Orion.Server? server = Player.Dimension?.World?.Server as global::Orion.Server;
            double tps = server?.Tps ?? TargetTps;
            double mspt = Player.Dimension?.World is Tickable tickable ? tickable.TickWork : 0;
            _averageMspt = _averageMspt == 0 ? mspt : _averageMspt + ((mspt - _averageMspt) * 0.2);
            double workingSetMb = Environment.WorkingSet / (1024.0 * 1024.0);
            int chunksLoaded = Player.Dimension?.ChunkCount ?? 0;

            StringBuilder builder = new();
            builder.AppendLine($"TPS {tps:0.0}/{TargetTps:0.0}");
            builder.AppendLine($"MSPT {FormatMs(mspt)} avg {FormatMs(_averageMspt)}");
            builder.AppendLine($"RAM {workingSetMb:0.0}MB chunks {chunksLoaded} players {server?.Sessions.Count ?? 0}");
            AppendWorkerStats(builder, server, _mode);

            PlayerChunkRenderingTrait? chunkView = Player.GetTrait<PlayerChunkRenderingTrait>();
            if (chunkView is not null)
            {
                builder.AppendLine(chunkView.FormatDebugHudLine());
            }

            if (Player.Dimension?.World?.AttachedWorkerId is int worldWorkerId)
            {
                builder.AppendLine($"sim ww{worldWorkerId}");
            }

            TextPacket packet = new()
            {
                NeedsTranslation = false,
                VariantType = TextVariantType.MessageOnly,
                Variant = new TextVariant
                {
                    Type = TextType.Tip,
                    Message = builder.ToString().TrimEnd('\n', '\r')
                },
                Xuid = string.Empty,
                PlatformChatId = string.Empty,
                FilteredMessage = null
            };

            Player.Send(packet);
            _lastSentTick = details.CurrentTick;
        }
        catch (Exception exception)
        {
            Warn($"[{Player.Username}] DebugTrait exception: {exception}");
        }
    }

    public override EntityTrait Clone(Entity entity)
    {
        return new DebugTrait(entity);
    }

    public void SetMode(DebugHudMode mode)
    {
        _mode = mode;
    }

    public DebugHudMode GetMode()
    {
        return _mode;
    }

    private static void AppendWorkerStats(StringBuilder builder, global::Orion.Server? server, DebugHudMode mode)
    {
        if (server is null)
        {
            return;
        }

        IReadOnlyList<WorkerLoadMetrics> netMetrics = server.Scheduler.GetMetrics();
        if (mode == DebugHudMode.Full)
        {
            if (netMetrics.Count > 0)
            {
                builder.Append("net");
                for (int i = 0; i < netMetrics.Count; i++)
                {
                    WorkerLoadMetrics net = netMetrics[i];
                    int pending = server.Scheduler is SingleThreadScheduler single ? single.PendingMessageCount : 0;
                    builder.Append($" w{net.WorkerId} t{net.Tps:0.0} m{FormatMs(net.LastTickWorkMs)} q{pending}");
                }
                builder.AppendLine();
            }
        }
        else if (netMetrics.Count > 0)
        {
            WorkerLoadMetrics net = netMetrics[0];
            int pending = server.Scheduler is SingleThreadScheduler single ? single.PendingMessageCount : 0;
            builder.AppendLine($"net w{net.WorkerId} tps {net.Tps:0.0} work {FormatMs(net.LastTickWorkMs)} q {pending}");
        }

        IReadOnlyList<AreaWorkerLoadMetrics> areaMetrics = server.AreaScheduler.GetMetrics();
        if (server.ConnectionCoordinator is ConnectionCoordinator coordinator && coordinator.IsActive)
        {
            if (mode == DebugHudMode.Full)
            {
                if (areaMetrics.Count > 0)
                {
                    builder.Append("area");
                    for (int i = 0; i < areaMetrics.Count; i++)
                    {
                        AreaWorkerLoadMetrics area = areaMetrics[i];
                        builder.Append($" aw{area.WorkerId} t{area.Tps:0.0} m{FormatMs(area.LastTickWorkMs)} l{FormatMs(area.TickLagMs)}");
                    }
                    builder.AppendLine();
                }

                builder.Append("sess");
                for (int workerId = 0; workerId < coordinator.Pool.WorkerCount; workerId++)
                {
                    SessionWorker worker = coordinator.Pool.GetWorker(workerId);
                    builder.Append($" sw{workerId} s{worker.SessionCount} q{worker.PendingMessageCount} m{FormatMs(worker.LastTickWorkMs)}");
                }
                builder.AppendLine();
            }
            else
            {
                if (areaMetrics.Count > 0)
                {
                    AreaWorkerLoadMetrics hottest = areaMetrics[0];
                    for (int i = 1; i < areaMetrics.Count; i++)
                    {
                        if (areaMetrics[i].LastTickWorkMs > hottest.LastTickWorkMs)
                        {
                            hottest = areaMetrics[i];
                        }
                    }

                    builder.AppendLine($"area n{areaMetrics.Count} hot aw{hottest.WorkerId} {FormatMs(hottest.LastTickWorkMs)} lag {FormatMs(hottest.TickLagMs)}");
                }

                int sessions = 0;
                int pending = 0;
                double hottestSessionMs = 0;
                int hottestWorkerId = 0;
                for (int workerId = 0; workerId < coordinator.Pool.WorkerCount; workerId++)
                {
                    SessionWorker worker = coordinator.Pool.GetWorker(workerId);
                    sessions += worker.SessionCount;
                    pending += worker.PendingMessageCount;
                    if (worker.LastTickWorkMs > hottestSessionMs)
                    {
                        hottestSessionMs = worker.LastTickWorkMs;
                        hottestWorkerId = workerId;
                    }
                }

                builder.AppendLine($"sess n{coordinator.Pool.WorkerCount} online {sessions} q {pending} hot sw{hottestWorkerId} {FormatMs(hottestSessionMs)}");
            }
        }
    }

    private static string FormatMs(double value)
    {
        if (value <= 0)
        {
            return "0.000";
        }

        return value < 0.001 ? "<0.001" : value.ToString("0.000");
    }
}

public enum DebugHudMode
{
    Off,
    Simplified,
    Full
}
