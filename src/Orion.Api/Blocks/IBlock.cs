namespace Orion.Api.Blocks;

public interface IBlockType
{
    string Identifier { get; }
}

public interface IBlockPermutation
{
    IBlockType Type { get; }
}

public interface IBlock
{
    IBlockType Type { get; }
    IBlockPermutation Permutation { get; }
}
