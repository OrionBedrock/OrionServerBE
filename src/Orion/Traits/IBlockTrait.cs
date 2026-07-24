namespace Orion.Traits;

/// <summary>Extension hook for block behavior (plugins). Core does not implement vanilla mechanics.</summary>
public interface IBlockTrait
{
    void OnPlace();

    void OnBreak();
}

/// <summary>Default no-op block trait base.</summary>
public abstract class BlockTrait : IBlockTrait
{
    public virtual void OnPlace()
    {
    }

    public virtual void OnBreak()
    {
    }
}
