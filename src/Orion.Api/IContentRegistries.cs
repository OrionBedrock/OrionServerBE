namespace Orion.Api;

/// <summary>Thin content-registry lookup surface for plugins.</summary>
public interface IContentRegistries
{
    /// <summary>Whether a block id is registered.</summary>
    bool HasBlock(string id);

    /// <summary>Whether an item id is registered.</summary>
    bool HasItem(string id);
}
