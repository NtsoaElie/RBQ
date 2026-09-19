namespace Rbq.Ingestion;

public sealed class DocumentPage
{
    public required string Text { get; init; }
    public required int Number { get; init; }
    public required string Filename { get; init; }
}
