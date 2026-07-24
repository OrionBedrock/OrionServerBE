using EntityHandle = Orion.Entity.Entity;

namespace Orion.Entity.Animation;

/// <summary>Client presentation hooks driven by animation controllers (no combat formulas).</summary>
public interface IAnimationEffectSink
{
    void PlayAnimation(EntityHandle entity, string animationName);

    void SpawnParticle(EntityHandle entity, string particleName, double x, double y, double z);

    void PlaySound(EntityHandle entity, string soundName, double x, double y, double z);
}

/// <summary>No-op sink used when no client broadcast is wired.</summary>
public sealed class NullAnimationEffectSink : IAnimationEffectSink
{
    public static NullAnimationEffectSink Instance { get; } = new();

    public void PlayAnimation(EntityHandle entity, string animationName)
    {
    }

    public void SpawnParticle(EntityHandle entity, string particleName, double x, double y, double z)
    {
    }

    public void PlaySound(EntityHandle entity, string soundName, double x, double y, double z)
    {
    }
}

/// <summary>Records effect calls for tests.</summary>
public sealed class RecordingAnimationEffectSink : IAnimationEffectSink
{
    public List<string> Animations { get; } = [];
    public List<string> Particles { get; } = [];
    public List<string> Sounds { get; } = [];

    public void PlayAnimation(EntityHandle entity, string animationName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(animationName);
        Animations.Add(animationName);
    }

    public void SpawnParticle(EntityHandle entity, string particleName, double x, double y, double z)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(particleName);
        Particles.Add($"{particleName}@{x:0.##},{y:0.##},{z:0.##}");
    }

    public void PlaySound(EntityHandle entity, string soundName, double x, double y, double z)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);
        Sounds.Add($"{soundName}@{x:0.##},{y:0.##},{z:0.##}");
    }
}
