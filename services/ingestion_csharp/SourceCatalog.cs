using System.Text.Json;
using System.Text.Json.Serialization;
using Rbq.Contracts;

namespace Rbq.Ingestion;

/// <summary>Reviewed source inventory. Downloading/crawling is separate from parsing.</summary>
public sealed class SourceCatalog
{
    public static DocumentSource Find(string catalogPath, string documentId)
    {
        var jsonOptions = new JsonSerializerOptions(ChunkJson.Options);
        jsonOptions.Converters.Add(new JsonStringEnumConverter<DocumentKind>(
            JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        List<DocumentSource> sources = JsonSerializer.Deserialize<List<DocumentSource>>(
            File.ReadAllText(catalogPath), jsonOptions)
            ?? throw new InvalidDataException("Expected a source catalog array.");

        if (sources.Any(source => string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.Title)))
            throw new InvalidDataException("Catalog entries require an ID and title.");
        if (sources.GroupBy(source => source.Id).Any(group => group.Count() > 1))
            throw new InvalidDataException("Duplicate document IDs in catalog.");

        return sources.SingleOrDefault(source => source.Id == documentId)
            ?? throw new ArgumentException($"Unknown catalog document: {documentId}");
    }
}
