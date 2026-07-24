using Orion.Config;
using Orion.Network;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Nbt;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;

namespace Orion.Player;

/// <summary>
/// Builds and sends the minimal StartGame → registries → PlayerSpawn sequence.
/// </summary>
public static class PlayerSpawnPipeline
{
    public static void SendSpawnSequence(ServerContext context, Player player)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(player);

        StartGamePacket startGame = BuildStartGame(context.Config, player);
        var actorIds = new AvailableActorIdentifiersPacket { Data = new CompoundTag() };
        var itemRegistry = new ItemRegistryPacket { Items = [] };
        var creative = new CreativeContentPacket { Groups = [], Items = [] };
        var spawnStatus = new PlayStatusPacket(PlayStatus.PlayerSpawn);

        context.Sender.Send(player.Session.Connection, startGame);
        context.Sender.Send(player.Session.Connection, itemRegistry);
        context.Sender.Send(player.Session.Connection, actorIds);
        context.Sender.Send(player.Session.Connection, spawnStatus);
        context.Sender.Send(player.Session.Connection, creative);

        player.Session.State = SessionState.InGame;
    }

    public static StartGamePacket BuildStartGame(OrionConfig config, Player player)
    {
        WorldDefaultSettingsConfig worldSettings = config.Server.WorldDefaultSettings;
        string worldName = string.IsNullOrWhiteSpace(config.Server.Name) ? "OrionServer" : config.Server.Name;

        return new StartGamePacket
        {
            EntityUniqueId = player.UniqueId,
            EntityRuntimeId = player.RuntimeId,
            PlayerGameMode = 0,
            PlayerPosition = new Vec3f(player.SpawnX, player.SpawnY, player.SpawnZ),
            Pitch = 0f,
            Yaw = 0f,
            WorldSeed = worldSettings.Seed,
            SpawnBiomeType = SpawnBiomeType.Default,
            UserDefinedBiomeName = "plains",
            Dimension = player.Dimension.Type,
            Generator = 1,
            WorldGameMode = 0,
            Hardcore = false,
            Difficulty = 1,
            WorldSpawn = new BlockPos
            {
                X = (int)Math.Floor(player.SpawnX),
                Y = (int)Math.Floor(player.SpawnY),
                Z = (int)Math.Floor(player.SpawnZ),
            },
            AchievementsDisabled = true,
            EditorWorldType = EditorWorldType.NotEditor,
            CreatedInEditor = false,
            ExportedFromEditor = false,
            DayCycleLockTime = 6000,
            EducationEditionOffer = 0,
            EducationFeaturesEnabled = false,
            EducationProductId = string.Empty,
            RainLevel = 0f,
            LightningLevel = 0f,
            ConfirmedPlatformLockedContent = false,
            MultiPlayerGame = true,
            LanBroadcastEnabled = false,
            XblBroadcastMode = XblBroadcastMode.Public,
            PlatformBroadcastMode = (int)XblBroadcastMode.Public,
            CommandsEnabled = true,
            TexturePackRequired = false,
            GameRules = [],
            Experiments = [],
            ExperimentsPreviouslyToggled = false,
            BonusChestEnabled = false,
            StartWithMapEnabled = false,
            PlayerPermissions = 1,
            ServerChunkTickRadius = 4,
            HasLockedBehaviourPack = false,
            HasLockedTexturePack = false,
            FromLockedWorldTemplate = false,
            MsaGamerTagsOnly = false,
            FromWorldTemplate = false,
            WorldTemplateSettingsLocked = false,
            OnlySpawnV1Villagers = false,
            PersonaDisabled = false,
            CustomSkinsDisabled = false,
            EmoteChatMuted = false,
            BaseGameVersion = Constants.MinecraftVersion,
            LimitedWorldWidth = 0,
            LimitedWorldDepth = 0,
            NewNether = true,
            EducationSharedResourceUri = new EducationSharedResourceUri
            {
                ButtonName = string.Empty,
                LinkUri = string.Empty,
            },
            ForceExperimentalGameplay = new Optional<BoolType> { HasValue = false },
            ChatRestrictionLevel = ChatRestrictionLevel.None,
            DisablePlayerInteractions = false,
            LevelId = worldSettings.Identifier,
            WorldName = worldName,
            TemplateContentIdentity = string.Empty,
            Trial = false,
            PlayerMovementSettings = new PlayerMovementSettings
            {
                RewindHistorySize = 100,
                ServerAuthoritativeBlockBreaking = true,
            },
            Time = 0,
            EnchantmentSeed = 0,
            Blocks = [],
            MultiPlayerCorrelationId = Guid.NewGuid().ToString(),
            ServerAuthoritativeInventory = true,
            GameVersion = Constants.MinecraftVersion,
            PropertyData = new CompoundTag(),
            ServerBlockStateChecksum = 0,
            WorldTemplateId = Guid.Empty,
            ClientSideGeneration = false,
            UseBlockNetworkIdHashes = true,
            ServerAuthoritativeSound = true,
            ServerJoinInformation = new OptionalValue<ServerJoinInformation> { HasValue = false },
            ServerId = string.Empty,
            ScenarioId = string.Empty,
            WorldId = string.Empty,
            OwnerId = player.Session.Xuid ?? string.Empty,
        };
    }
}
