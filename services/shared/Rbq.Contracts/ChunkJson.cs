using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rbq.Contracts;

/// <summary>Preserves snake_case metadata, including existing Python exports.</summary>
public static class ChunkJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter<SourceType>(
            JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        return options;
    }

    public static List<Chunk> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<List<Chunk>>(json, Options)
            ?? throw new InvalidDataException("Expected a JSON array of chunks.");
    }
}
