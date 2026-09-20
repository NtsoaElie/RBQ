namespace Rbq.Ingestion;

public sealed class ExcludedBlock
{
    public required TextBlock Block { get; init; }
    public required string Reason { get; init; }
}
