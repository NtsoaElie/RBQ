namespace Rbq.Contracts;

/// <summary>The same source object is used by ingestion and question generation.</summary>
public sealed class Chunk
{
    public required string ChunkId { get; init; }
    public required string DocumentId { get; init; }
    public required string DocumentVersion { get; init; }
    public string? ProfileId { get; init; }
    public required SourceType SourceType { get; init; }
    public string? SourceUrl { get; init; }
    public required string Filename { get; init; }
    public required int PageStart { get; init; }
    public required int PageEnd { get; init; }
    public int PageNumber => PageStart;
    public Competency? Module { get; init; }
    public Competency? Competency { get; init; }
    public Competency? Skill { get; init; }
    public List<string> SkillIds { get; init; } = [];
    public List<string> Breadcrumb { get; init; } = [];
    public required string Content { get; init; }
    public List<string> ReviewIssues { get; init; } = [];
    public string ParserVersion { get; init; } = "rbq-csharp-v1";

    public string EmbeddingText => string.Join(" > ", Breadcrumb) + "\n" + Content;
}
