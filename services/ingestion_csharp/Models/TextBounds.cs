namespace Rbq.Ingestion;

// Coordinates are normalized to [0,1], with the origin at the top left.
public sealed class TextBounds
{
    public required double Left { get; init; }
    public required double Top { get; init; }
    public required double Right { get; init; }
    public required double Bottom { get; init; }
}
