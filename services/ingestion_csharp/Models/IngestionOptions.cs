using Rbq.Contracts;

namespace Rbq.Ingestion;

public sealed class IngestionOptions
{
    public DocumentKind? Kind { get; init; }
    public DocumentSource? Source { get; init; }
    public string? Title { get; init; }
    public required string DocumentId { get; init; }
    public string? ProfileId { get; init; }
    public required SourceType SourceType { get; init; }
    public string? SourceUrl { get; init; }
    public List<string> SkillIds { get; init; } = [];
    public int MaxCharacters { get; init; } = 2000;
}
