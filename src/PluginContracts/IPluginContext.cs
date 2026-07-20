using Orion.Api;
using Orion.PluginContracts.Events;
using Orion.PluginContracts.Messaging;
using Orion.PluginContracts.Network;
using Orion.PluginContracts.Registry;
using Orion.PluginContracts.Services;

namespace Orion.PluginContracts;

public interface IPluginContext
{
    IPluginManifest Manifest { get; }
    IServer Server { get; }
    IServiceRegistry Services { get; }
    IPluginMessenger Messenger { get; }
    IEventBus Events { get; }
    IContentRegistries Registries { get; }
    IPacketPipeline Packets { get; }
}
