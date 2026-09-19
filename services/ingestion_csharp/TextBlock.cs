namespace Rbq.Ingestion;

/// <summary>A line of source text in native reading order, with optional PDF geometry.</summary>
public sealed class TextBlock
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required int PageNumber { get; init; }
    public TextBounds? Bounds { get; init; }
    public double? FontSize { get; init; }
    public bool StartsParagraph { get; init; }
}

// Coordinates are normalized to [0,1], with the origin at the top left.
public sealed class TextBounds
{
    public required double Left { get; init; }
    public required double Top { get; init; }
    public required double Right { get; init; }
    public required double Bottom { get; init; }
}
