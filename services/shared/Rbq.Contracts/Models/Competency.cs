namespace Rbq.Contracts;

/// <summary>A module, competency element, or required skill from a profile.</summary>
public sealed class Competency
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required string Label { get; init; }
}
