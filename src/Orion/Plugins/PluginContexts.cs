using Orion.Api;
using Orion.PluginContracts;
using Orion.PluginContracts.Events;
using Orion.PluginContracts.Messaging;
using Orion.PluginContracts.Network;
using Orion.PluginContracts.Registry;
using Orion.PluginContracts.Services;

namespace Orion.Plugins;

internal sealed class PluginLoadContext(IPluginManifest manifest, IContentRegistries registries) : IPluginLoadContext
{
    public IPluginManifest Manifest { get; } = manifest;
    public IContentRegistries Registries { get; } = registries;
}

internal sealed class PluginContext(
    IPluginManifest manifest,
    IServer server,
    IServiceRegistry services,
    IPluginMessenger messenger,
    IEventBus events,
    IContentRegistries registries,
    IPacketPipeline packets) : IPluginContext
{
    public IPluginManifest Manifest { get; } = manifest;
    public IServer Server { get; } = server;
    public IServiceRegistry Services { get; } = services;
    public IPluginMessenger Messenger { get; } = messenger;
    public IEventBus Events { get; } = events;
    public IContentRegistries Registries { get; } = registries;
    public IPacketPipeline Packets { get; } = packets;
}

internal sealed class WorldInitContext(
    IPluginManifest manifest,
    IOrionWorld world,
    IContentRegistries registries) : IWorldInitContext
{
    public IPluginManifest Manifest { get; } = manifest;
    public IOrionWorld World { get; } = world;
    public IContentRegistries Registries { get; } = registries;
}

internal sealed class StubOrionWorld : IOrionWorld;
