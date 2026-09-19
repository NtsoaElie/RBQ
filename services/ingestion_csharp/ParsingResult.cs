using Rbq.Contracts;

namespace Rbq.Ingestion;

public sealed class ParsingResult
{
    public List<Chunk> Chunks { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<TextBlock> UnclassifiedBlocks { get; } = [];
    public List<ExcludedBlock> ExcludedBlocks { get; } = [];
    public string ParserVersion { get; init; } = ChunkFactory.ParserVersion;

    public void Exclude(TextBlock block, string reason)
    {
        ExcludedBlocks.Add(new ExcludedBlock { Block = block, Reason = reason });
    }
}

public sealed class ExcludedBlock
{
    public required TextBlock Block { get; init; }
    public required string Reason { get; init; }
}
