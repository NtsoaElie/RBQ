namespace Rbq.Ingestion;

public sealed class ExtractedDocument
{
    public required DocumentSource Source { get; init; }
    public required string Filename { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyList<TextBlock> Blocks { get; init; }
}
