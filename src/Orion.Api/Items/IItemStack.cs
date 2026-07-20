namespace Orion.Api.Items;

public interface IItemType
{
    string Identifier { get; }
}

public interface IItemStack
{
    IItemType Type { get; }
    int Count { get; }
}
