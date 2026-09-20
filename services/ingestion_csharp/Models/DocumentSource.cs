namespace Rbq.Ingestion;

/// <summary>One catalog entry; a source may serve several exams or licences.</summary>
public sealed class DocumentSource
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Url { get; init; }
    public required DocumentKind Kind { get; init; }
    public List<string> ProfileIds { get; init; } = [];
    public List<string> LicenceSubcategories { get; init; } = [];
}
