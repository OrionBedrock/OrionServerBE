namespace Orion.Api;

/// <summary>Options for <see cref="IDimension.SpawnEntity"/>.</summary>
public readonly record struct EntitySpawnOptions(bool InitialSpawn = true);
