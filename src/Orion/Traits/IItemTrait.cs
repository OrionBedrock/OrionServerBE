namespace Orion.Traits;

/// <summary>Extension hook for item behavior (plugins). Core does not implement vanilla mechanics.</summary>
public interface IItemTrait
{
    void OnUse();
}

/// <summary>Default no-op item trait base.</summary>
public abstract class ItemTrait : IItemTrait
{
    public virtual void OnUse()
    {
    }
}
