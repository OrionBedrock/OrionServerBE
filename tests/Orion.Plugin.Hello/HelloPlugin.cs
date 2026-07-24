using Orion.Api;

namespace Hello;

/// <summary>Test-only plugin — not shipped with the dedicated server.</summary>
public sealed class HelloPlugin : Plugin
{
    public string Lifecycle { get; private set; } = "";

    public string? SeenServerName { get; private set; }

    public bool SawGlobalScheduler { get; private set; }

    public bool SawRegionScheduler { get; private set; }

    public bool SawRegistries { get; private set; }

    public bool SawDirt { get; private set; }

    public override void OnLoad()
    {
        Lifecycle += "OnLoad;";
        SeenServerName = Server.Name;
    }

    public override void OnStart()
    {
        Lifecycle += "OnStart;";
        SawGlobalScheduler = Server.GlobalScheduler is not null;
        SawRegionScheduler = Server.RegionScheduler is not null;
        SawRegistries = Server.Registries is not null;
        SawDirt = Server.Registries?.HasBlock("minecraft:dirt") == true;
    }

    public override void OnDisable()
    {
        Lifecycle += "OnDisable;";
    }
}
