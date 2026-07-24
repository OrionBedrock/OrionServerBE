using Orion.Config;
using Orion.Network;
using Orion.Network.Handlers;
using Orion.Permissions;
using Orion.Player;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.Region;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class PermissionGateTests
{
    [Fact]
    public void LoadSample_DefaultPlayerCannotBreak()
    {
        string path = FindPermissionsSample();
        PermissionService service = PermissionService.Load(path);
        ResolvedPermissions resolved = service.Resolve("Steve", xuid: null);
        Assert.False(resolved.IsOperator);
        Assert.False(resolved.Has(PermissionNodes.BlockBreak));
        Assert.False(resolved.Has(PermissionNodes.BlockPlace));
        Assert.True(resolved.Has(PermissionNodes.Command));
    }

    [Fact]
    public void OpInList_GetsBreakAndPlace()
    {
        string path = WriteTempPermissions(
            """
            {
              "ops": ["Steve"],
              "permissions": {
                "orion.command": true,
                "orion.block.break": false,
                "orion.block.place": false
              },
              "groups": {
                "default": { "permissions": ["orion.command"] },
                "admin": {
                  "permissions": [
                    "orion.command",
                    "orion.block.break",
                    "orion.block.place",
                    "orion.admin"
                  ]
                }
              },
              "players": {}
            }
            """);

        try
        {
            PermissionService service = PermissionService.Load(path);
            ResolvedPermissions resolved = service.Resolve("Steve", xuid: null);
            Assert.True(resolved.IsOperator);
            Assert.True(resolved.Has(PermissionNodes.BlockBreak));
            Assert.True(resolved.Has(PermissionNodes.BlockPlace));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AuthInput_NonOpDestroy_IsDiscarded()
    {
        var (context, session, _) = CreatePlayer(isOp: false);
        var packet = new PlayerAuthInputPacket
        {
            BlockActions =
            [
                new PlayerBlockAction { Action = PlayerActionType.StartDestroyBlock },
            ],
        };

        PermissionGateResult result = PlayerAuthInputHandler.Handle(context, session, packet);
        Assert.Equal(PermissionGateResult.Discarded, result);
    }

    [Fact]
    public void AuthInput_OpDestroy_IsAllowed()
    {
        var (context, session, player) = CreatePlayer(isOp: true);
        Assert.True(player.HasPermission(PermissionNodes.BlockBreak));

        var packet = new PlayerAuthInputPacket
        {
            BlockActions =
            [
                new PlayerBlockAction { Action = PlayerActionType.ContinueDestroyBlock },
            ],
        };

        PermissionGateResult result = PlayerAuthInputHandler.Handle(context, session, packet);
        Assert.Equal(PermissionGateResult.Allowed, result);
    }

    [Fact]
    public void InventoryTransaction_NonOpUseItem_IsDiscarded()
    {
        var (context, session, _) = CreatePlayer(isOp: false);
        var packet = new InventoryTransactionPacket
        {
            TransactionData = new UseItemInventoryTransactionData(),
        };

        PermissionGateResult result = InventoryTransactionHandler.Handle(context, session, packet);
        Assert.Equal(PermissionGateResult.Discarded, result);
    }

    [Fact]
    public void InventoryTransaction_OpUseItem_IsAllowed()
    {
        var (context, session, _) = CreatePlayer(isOp: true);
        var packet = new InventoryTransactionPacket
        {
            TransactionData = new UseItemInventoryTransactionData(),
        };

        PermissionGateResult result = InventoryTransactionHandler.Handle(context, session, packet);
        Assert.Equal(PermissionGateResult.Allowed, result);
    }

    [Fact]
    public void StartGame_OpGetsElevatedPlayerPermissions()
    {
        var config = new OrionConfig();
        var (_, _, player) = CreatePlayer(isOp: true);
        StartGamePacket start = PlayerSpawnPipeline.BuildStartGame(config, player);
        Assert.Equal(2, start.PlayerPermissions);
    }

    private static (ServerContext Context, ConnectionSession Session, Player.Player Player) CreatePlayer(bool isOp)
    {
        var config = new OrionConfig
        {
            Server = new ServerRootConfig
            {
                WorldDefaultSettings = new WorldDefaultSettingsConfig
                {
                    Dimensions = [new DimensionConfig { Identifier = "overworld", SpawnPosition = [0, 64, 0] }],
                },
            },
        };

        var document = new PermissionDocument
        {
            Ops = isOp ? ["Steve"] : [],
            Permissions =
            {
                [PermissionNodes.Command] = true,
                [PermissionNodes.BlockBreak] = false,
                [PermissionNodes.BlockPlace] = false,
            },
            Groups =
            {
                ["default"] = new PermissionGroup { Permissions = [PermissionNodes.Command] },
                ["admin"] = new PermissionGroup
                {
                    Permissions =
                    [
                        PermissionNodes.Command,
                        PermissionNodes.BlockBreak,
                        PermissionNodes.BlockPlace,
                        PermissionNodes.Admin,
                    ],
                },
            },
        };
        var permissions = new PermissionService(document);

        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        using var provider = new InMemoryWorldProvider();
        using var world = Orion.World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var sessions = new SessionManager();
        var session = sessions.Create(new StubNet());
        session.Username = "Steve";
        session.State = SessionState.InGame;

        var players = new PlayerManager();
        Player.Player player = players.Create(
            session,
            world,
            regionizer,
            config.Server.WorldDefaultSettings.Dimensions[0],
            permissions);

        var context = new ServerContext(
            config,
            sessions,
            new PacketSender(config, new NoopSend()),
            new SessionPacketQueue(),
            new SessionWorkQueue(),
            players,
            world,
            regionizer,
            permissions);

        return (context, session, player);
    }

    private static string WriteTempPermissions(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "orion-perms-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string FindPermissionsSample()
    {
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "permissions.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "config", "permissions.json")),
            Path.GetFullPath("./config/permissions.json"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fallback: embed the sample defaults so the test still validates resolve.
        return WriteTempPermissions(
            """
            {
              "ops": [],
              "permissions": {
                "orion.command": true,
                "orion.block.break": false,
                "orion.block.place": false
              },
              "groups": {
                "default": { "permissions": ["orion.command"] },
                "admin": {
                  "permissions": ["orion.command", "orion.block.break", "orion.block.place", "orion.admin"]
                }
              },
              "players": {}
            }
            """);
    }

    private sealed class StubNet : RakNet.NetworkConnection
    {
        protected override void SendMessage(ReadOnlySpan<byte> payload)
        {
        }
    }

    private sealed class NoopSend : INetworkSend
    {
        public void Send(RakNet.NetworkConnection connection, ReadOnlySpan<byte> payload, RakNet.Packets.Enums.Reliability reliability, bool immediate = false)
        {
        }

        public void Disconnect(RakNet.NetworkConnection connection)
        {
        }
    }
}
